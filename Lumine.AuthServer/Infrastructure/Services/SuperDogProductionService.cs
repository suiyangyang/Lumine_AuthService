using System.Security.Cryptography;
using Lumine.AuthServer.Application.DTOs.SuperDog;
using Lumine.AuthServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumine.AuthServer.Infrastructure.Services;

public sealed record ProductionActor(string Subject, string UserName);

public sealed class SuperDogProductionService(AuthDbContext db, ProductionValidator validator, ManufacturerKeyProtector protector,
    ILogger<SuperDogProductionService> logger)
{
    public async Task<ManufacturerKeyInfo> EnsureKeyAsync(ushort manufacturerId, ProductionActor actor, CancellationToken ct)
    {
        validator.ValidateManufacturer(manufacturerId);
        // 数据库唯一约束处理跨进程并发；重试时只返回已提交的同一 KEY 标识。
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var secret = await FindKey(manufacturerId, ct);
                    if (secret == null)
                    {
                        secret = CreateKey(manufacturerId, actor);
                    }
                    if (!secret.IsActive) throw new InvalidDataException("厂家 KEY 已停用。");
                    Audit(actor, "KEY创建确认", manufacturerId.ToString(), "成功");
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return new ManufacturerKeyInfo(manufacturerId, secret.Id.ToString("D"), secret.Version);
                });
            }
            catch (DbUpdateException e) when (Duplicate(e) && attempt < 2) { db.ChangeTracker.Clear(); }
        }
    }

    public async Task<ManufacturerKey?> RetrieveKeyAsync(ushort manufacturerId, bool copy, ProductionActor actor, CancellationToken ct)
    {
        validator.ValidateManufacturer(manufacturerId);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var key = await FindKey(manufacturerId, ct);
                    // view 由接口实时鉴权；自动创建另查实时角色权限，不信任 token 权限快照。
                    if (key == null && !copy && Guid.TryParse(actor.Subject, out var subject) &&
                        await db.UserRoles.Where(x => x.UserId == subject && x.User.IsActive)
                            .SelectMany(x => x.Role.RolePermissions)
                            .AnyAsync(x => x.PermissionName == "dongle.activation.manage", ct))
                    {
                        key = CreateKey(manufacturerId, actor);
                    }
                    if (key == null || !key.IsActive)
                    {
                        Audit(actor, copy ? "KEY复制" : "KEY查看", manufacturerId.ToString(), "拒绝", "key_unavailable");
                        await db.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);
                        return null;
                    }
                    var plainText = protector.Unprotect(key);
                    Audit(actor, copy ? "KEY复制" : "KEY查看", manufacturerId.ToString(), "成功");
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return new ManufacturerKey(manufacturerId, key.Id.ToString("D"), key.Version, plainText);
                });
            }
            catch (DbUpdateException e) when (Duplicate(e) && attempt < 2) { db.ChangeTracker.Clear(); }
        }
    }

    private ManufacturerSecret CreateKey(ushort manufacturerId, ProductionActor actor)
    {
        var secret = new ManufacturerSecret
        {
            Id = Guid.NewGuid(), VendorId = validator.VendorId, ManufacturerId = manufacturerId,
            CreatedAtUtc = DateTime.UtcNow
        };
        var text = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        secret.ProtectedKey = protector.Protect(secret, text);
        secret.KeyLookupHmac = protector.Lookup(secret.VendorId, text);
        db.Set<ManufacturerSecret>().Add(secret);
        Audit(actor, "KEY创建", manufacturerId.ToString(), "成功");
        return secret;
    }

    public async Task<UploadResult> UploadAsync(ProductionUpload record, ProductionActor actor, CancellationToken ct)
    {
        string operation = record?.OperationId ?? "";
        ValidatedProduction material;
        try { material = validator.Validate(record!); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or FormatException or CryptographicException or System.Xml.XmlException)
        {
            var code = e is ArgumentException { ParamName: nameof(WriteForm.OperatorPinyin) }
                ? "invalid_operator_pinyin" : "invalid_material";
            await RejectionAsync(actor, code, ct);
            return new(operation, false, null, "400 " + code);
        }
        catch (SuperDogUnavailableException)
        {
            await RejectionAsync(actor, "configuration_unavailable", ct);
            return new(operation, false, null, "503 configuration_unavailable");
        }
        record = material.Record;
        var vendor = validator.VendorId;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var previous = await db.Set<ProductionRegistration>().SingleOrDefaultAsync(
                        x => x.VendorId == vendor && x.OperationId == operation, ct);
                    if (previous != null)
                    {
                        bool same = previous.MaterialHash == material.MaterialHash;
                        Audit(actor, "生产上传", operation, same ? "幂等成功" : "拒绝", same ? "replay" : "operation_conflict");
                        await db.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);
                        return same ? new UploadResult(operation, true, previous.EntitlementId.ToString("D"), null)
                            : new UploadResult(operation, false, null, "409 operation_conflict");
                    }
                    var secret = await FindKey(record.Form.Fields.ManufacturerId, ct);
                    if (secret == null || !secret.IsActive || secret.Id.ToString("D") != record.KeyId || secret.Version != record.KeyVersion)
                    {
                        Audit(actor, "生产上传", operation, "拒绝", "manufacturer_key_mismatch");
                        await db.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);
                        return new UploadResult(operation, false, null, "400 manufacturer_key_mismatch");
                    }
                    var entitlement = await db.Set<DongleEntitlement>().SingleOrDefaultAsync(
                        x => x.VendorId == vendor && x.ActivationSourceHardwareKeyId == record.HardwareKeyId, ct);
                    if (entitlement != null)
                    {
                        // 存量资格没有可信修订时停止更新，不能按时间或到达次序猜测。
                        var conflict = entitlement.ProductionRevision < 1 ? "production_revision_unavailable"
                            : entitlement.ProductionRevision == record.ProductionRevision ||
                              await db.Set<ProductionRegistration>().AnyAsync(x => x.EntitlementId == entitlement.Id &&
                                  x.ProductionRevision == record.ProductionRevision, ct)
                                ? "production_revision_conflict" : null;
                        if (conflict != null)
                        {
                            Audit(actor, "生产上传", operation, "拒绝", conflict);
                            await db.SaveChangesAsync(ct);
                            await tx.CommitAsync(ct);
                            return new UploadResult(operation, false, null, "409 " + conflict);
                        }
                    }
                    if (entitlement == null)
                    {
                        entitlement = new DongleEntitlement
                        {
                            Id = Guid.NewGuid(), VendorId = vendor, ActivationSourceHardwareKeyId = record.HardwareKeyId
                        };
                        db.Set<DongleEntitlement>().Add(entitlement);
                    }
                    // 不写 BindingState/BindingRevision，不删除或重建原资格。
                    var advanced = entitlement.TryAdvanceProduction(record.ProductionRevision,
                        record.Form.Fields.ManufacturerId, record.PayloadDigest);
                    db.Set<ProductionRegistration>().Add(new ProductionRegistration
                    {
                        Id = Guid.NewGuid(), OperationId = operation, VendorId = vendor,
                        MaterialHash = material.MaterialHash, MaterialJson = material.MaterialJson,
                        Entitlement = entitlement, EntitlementId = entitlement.Id,
                        ProductionRevision = record.ProductionRevision,
                        UploadedBySubject = actor.Subject, UploadedAtUtc = DateTime.UtcNow
                    });
                    Audit(actor, "生产上传", operation, "成功", advanced ? "current_revision" : "historical_revision");
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return new UploadResult(operation, true, entitlement.Id.ToString("D"), null);
                });
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2) { db.ChangeTracker.Clear(); }
            catch (DbUpdateException e) when (Duplicate(e) && attempt < 2) { db.ChangeTracker.Clear(); }
            catch (Exception e) when (e is DbUpdateException or DbException or RetryLimitExceededException)
            {
                db.ChangeTracker.Clear();
                await RejectionAsync(actor, "persistence_failed", ct);
                return new(operation, false, null, "503 persistence_failed");
            }
        }
    }

    private Task<ManufacturerSecret?> FindKey(ushort manufacturerId, CancellationToken ct) =>
        db.Set<ManufacturerSecret>().SingleOrDefaultAsync(x => x.VendorId == validator.VendorId && x.ManufacturerId == manufacturerId, ct);

    private static bool Duplicate(DbUpdateException e) => e.InnerException is MySqlException { Number: 1062 };

    private async Task RejectionAsync(ProductionActor actor, string code, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        Audit(actor, "生产上传", "记录", "拒绝", code);
        try { await db.SaveChangesAsync(ct); }
        catch (Exception e) when (e is DbUpdateException or DbException or RetryLimitExceededException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("SuperDog production rejection audit could not be persisted.");
        }
    }

    private void Audit(ProductionActor actor, string action, string target, string outcome, string? code = null) =>
        db.AuditLogEntries.Add(new AuditLogEntry(Guid.NewGuid(), "SuperDog", action, actor.Subject,
            target, outcome, code, null, DateTime.UtcNow));
}
