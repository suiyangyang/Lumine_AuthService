using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.DTOs.SuperDog;
using Lumine.AuthServer.Controllers;
using Lumine.AuthServer.Domain.Entities;
using Lumine.AuthServer.Infrastructure;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// 无网络、无环境密钥、无 WebHost；测试密钥只存在于进程内。
var count = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAILED: " + name);
    count++;
}
void Reject(Action action, string name)
{
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or ArgumentException or FormatException or CryptographicException or System.Xml.XmlException or SuperDogUnavailableException)
    { count++; return; }
    throw new Exception("ACCEPTED INVALID INPUT: " + name);
}

using var signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var settings = new SuperDogOptions
{
    VendorId = "test-vendor",
    EncryptionKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    TrustedSigningKeys = [new() { VendorId = "test-vendor", SigningKeyId = 1, PublicKeyPem = signing.ExportSubjectPublicKeyInfoPem() }]
};
var options = Options.Create(settings);
var validator = new ProductionValidator(options);
const ulong hardware = 0xFEDCBA9876543210UL;
var fields = new DogFields(1, "0000000042", 4, Edition.Pro, ApplicationType.LC, ClientType.PC, 3);

// 独立构造协议字节，避免只用被测 Encode 验证自身 Decode。
ProductionUpload Material(string xml, DogFields formFields, byte signatureKey = 1)
{
    var ro = new byte[112];
    var rw = new byte[112];
    "SD41"u8.CopyTo(ro);
    ro[4] = 1; ro[5] = signatureKey;
    var payload = Encoding.UTF8.GetBytes(xml);
    BinaryPrimitives.WriteUInt16LittleEndian(ro.AsSpan(6), (ushort)payload.Length);
    BinaryPrimitives.WriteUInt64LittleEndian(ro.AsSpan(8), hardware);
    payload.CopyTo(rw, 0);
    var signed = ro[..16].Concat(payload).ToArray();
    signing.SignData(signed, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).CopyTo(ro, 16);
    return new("operation-1", "batch-1", hardware, new(formFields, "TEST"), Convert.ToBase64String(ro),
        Convert.ToBase64String(rw), Convert.ToHexString(SHA256.HashData(signed)), Guid.NewGuid().ToString("D"), 1,
        Guid.NewGuid().ToString("D"), "测试员", DateTimeOffset.UtcNow.AddMinutes(-1), 1);
}
const string canonical = "<Dog v=\"1\" sn=\"0000000042\" m=\"4\" e=\"0\" a=\"LC\" c=\"PC\" n=\"3\"/>";
var upload = Material(canonical, fields);
var accepted = validator.Validate(upload);
Check(accepted.Record.Form.Fields == fields, "valid signed fields");
foreach (var name in new[] { "A", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", new string('Z', 100) })
    Check(validator.Validate(upload with { Form = new(fields, name) }).Record.Form.OperatorPinyin == name,
        "uppercase pinyin valid boundaries");
foreach (var name in new string?[] { null, "", "test", "TESTa", "中文", "TEST\n", "TEST\r\n", " TEST", "TEST ",
    "TE ST", "TEST\t", "TEST1", "TEST-", "ＴＥＳＴ", new string('A', 101), new string('A', 128) })
{
    try
    {
        validator.Validate(upload with { Form = new(fields, name!) });
        throw new Exception("ACCEPTED INVALID PINYIN");
    }
    catch (ArgumentException e)
    {
        Check(e.ParamName == nameof(WriteForm.OperatorPinyin), "pinyin rejection has safe distinct error classification");
    }
}
var publicPem = settings.TrustedSigningKeys[0].PublicKeyPem;
settings.TrustedSigningKeys[0].PublicKeyPem = Convert.ToBase64String(signing.ExportSubjectPublicKeyInfo());
Check(validator.Validate(upload).MaterialHash == accepted.MaterialHash, "WPF exported base64 DER SPKI");
settings.TrustedSigningKeys[0].PublicKeyPem = " \n" + publicPem + "\n ";
Check(validator.Validate(upload).MaterialHash == accepted.MaterialHash, "PEM surrounding whitespace");
foreach (var invalidKey in new[] { "not-base64", Convert.ToBase64String(signing.ExportSubjectPublicKeyInfo().Concat(new byte[] { 0 }).ToArray()),
    Convert.ToBase64String(signing.ExportPkcs8PrivateKey()), signing.ExportPkcs8PrivateKeyPem() })
{
    settings.TrustedSigningKeys[0].PublicKeyPem = invalidKey;
    Reject(() => validator.Validate(upload), "invalid SPKI or private key config");
}
using (var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384))
{
    settings.TrustedSigningKeys[0].PublicKeyPem = Convert.ToBase64String(wrongCurve.ExportSubjectPublicKeyInfo());
    Reject(() => validator.Validate(upload), "non P256 SPKI config");
}
settings.TrustedSigningKeys[0].PublicKeyPem = publicPem;
Check(validator.Validate(upload).MaterialHash == accepted.MaterialHash, "stable replay material");
Check(validator.Validate(upload with { PayloadDigest = upload.PayloadDigest.ToLowerInvariant(), WrittenAt = upload.WrittenAt.ToOffset(TimeSpan.FromHours(8)) }).MaterialHash == accepted.MaterialHash,
    "digest case and equivalent instant canonicalization");
Check(validator.Validate(upload with { BatchId = "another-batch" }).MaterialHash != accepted.MaterialHash, "different metadata conflicts");
Check(validator.Validate(upload with { OperationId = "another-op" }).MaterialHash != accepted.MaterialHash, "operation identity in material");
Check(validator.Validate(upload with { ProductionRevision = 2 }).MaterialHash != accepted.MaterialHash,
    "production revision is immutable replay material");
Reject(() => validator.Validate(upload with { ProductionRevision = 0 }), "zero production revision");
Reject(() => validator.Validate(upload with { ProductionRevision = -1 }), "negative production revision");
ProductionRevisionChecks.Run(upload, validator, Check);

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var wire = JsonSerializer.Serialize(new ProductionBatchRequest([upload]), json);
Check(wire.Contains("\"hardwareKeyId\":18364758544493064720") && wire.Contains("\"edition\":0"), "UInt64 and numeric enums");
Check(JsonSerializer.Deserialize<ProductionBatchRequest>(wire, json)!.Records[0] == upload, "DTO round trip");
Check(wire.Contains("\"productionRevision\":1"), "production revision shared wire name");
Reject(() => validator.Validate(JsonSerializer.Deserialize<ProductionUpload>(
    JsonSerializer.Serialize(upload, json).Replace(",\"productionRevision\":1", ""), json)!), "missing production revision fails closed");
var keyWire = JsonSerializer.Serialize(new ManufacturerKey(4, "key", 1, "not-an-environment-key"), json);
Check(keyWire.Contains("\"plainText\"") && keyWire.Contains("\"manufacturerId\""), "Core key wire shape");

Reject(() => validator.Validate(upload with { HardwareKeyId = hardware - 1 }), "hardware mismatch");
Reject(() => validator.Validate(upload with { PayloadDigest = new string('0', 64) }), "digest mismatch");
Reject(() => validator.Validate(upload with { ReadOnlyBase64 = "invalid" }), "base64 length");
Reject(() => validator.Validate(upload with { Form = null! }), "null form");
Reject(() => validator.Validate(null!), "null record");
Reject(() => validator.Validate(upload with { OperationId = "bad\noperation" }), "operation control characters");
Reject(() => validator.Validate(upload with { WrittenAt = DateTimeOffset.UtcNow.AddHours(1) }), "future time");

DogFields[] changed = [fields with { InfoVersion = 2 }, fields with { SerialNumber = "0000000043" },
    fields with { ManufacturerId = 5 }, fields with { Edition = Edition.Server },
    fields with { ApplicationType = ApplicationType.GC }, fields with { ClientType = ClientType.Pad }, fields with { MaxInstrumentSets = 4 }];
foreach (var f in changed) Reject(() => validator.Validate(upload with { Form = new(f, "TEST") }), "seven field equality");
foreach (var xml in new[] { canonical.Replace("<Dog v", "<Dog  v"), canonical.Replace("v=\"1\"", "v=\"01\""),
    canonical.Replace("n=\"3\"", "n=\"0\""), canonical.Replace("m=\"4\"", "m=\"0\""),
    canonical.Replace("e=\"0\"", "e=\"3\""), canonical.Replace("sn=\"0000000042\"", "sn=\"42\""),
    canonical.Replace("/>", " extra=\"1\"/>"), "<?xml version=\"1.0\"?>" + canonical,
    canonical.Replace("/>", "></Dog>"), canonical + " ", canonical.Replace("LC", "ZZ"),
    canonical.Replace("m=\"4\"", "m=\"99\""), canonical.Replace("sn=\"0000000042\"", "sn=\"00000000&#52;2\"") })
    Reject(() => validator.Validate(Material(xml, fields)), "signed noncanonical XML");

// 每个字节逐一翻转：header、签名、硬件、XML、RO/RW 尾部必须全部拒绝。
for (int i = 0; i < 112; i++)
{
    var ro = Convert.FromBase64String(upload.ReadOnlyBase64); ro[i] ^= 1;
    Reject(() => validator.Validate(upload with { ReadOnlyBase64 = Convert.ToBase64String(ro) }), "RO tamper " + i);
    var rw = Convert.FromBase64String(upload.ReadWriteBase64); rw[i] ^= 1;
    Reject(() => validator.Validate(upload with { ReadWriteBase64 = Convert.ToBase64String(rw) }), "RW tamper " + i);
}
using (var untrusted = ECDsa.Create(ECCurve.NamedCurves.nistP256))
{
    var original = settings.TrustedSigningKeys[0].PublicKeyPem;
    settings.TrustedSigningKeys[0].PublicKeyPem = untrusted.ExportSubjectPublicKeyInfoPem();
    Reject(() => validator.Validate(upload), "untrusted production key");
    settings.TrustedSigningKeys[0].PublicKeyPem = original;
}
settings.TrustedSigningKeys.Add(settings.TrustedSigningKeys[0]);
Reject(() => validator.Validate(upload), "ambiguous configured key");
settings.TrustedSigningKeys.RemoveAt(1);
settings.VendorId = "other-vendor";
Reject(() => validator.Validate(upload), "vendor trust boundary");
settings.VendorId = "test-vendor";

var protector = new ManufacturerKeyProtector(options);
var secret = new ManufacturerSecret { Id = Guid.NewGuid(), VendorId = settings.VendorId, ManufacturerId = 4, Version = 1 };
const string plain = "test-only-plaintext";
secret.ProtectedKey = protector.Protect(secret, plain);
Check(!secret.ProtectedKey.Contains(plain) && protector.Unprotect(secret) == plain, "AES GCM round trip");
Check(protector.Protect(secret, plain) != secret.ProtectedKey, "random nonces");
Check(protector.Lookup(settings.VendorId, plain) != protector.Lookup("other", plain), "vendor scoped HMAC");
secret.ManufacturerId++;
Reject(() => protector.Unprotect(secret), "AAD manufacturer swap"); secret.ManufacturerId--;
secret.Version++;
Reject(() => protector.Unprotect(secret), "AAD version swap"); secret.Version--;
var idBefore = secret.Id; secret.Id = Guid.NewGuid();
Reject(() => protector.Unprotect(secret), "AAD key identity swap"); secret.Id = idBefore;
secret.VendorId = "other";
Reject(() => protector.Unprotect(secret), "AAD vendor swap"); secret.VendorId = settings.VendorId;
var encrypted = Convert.FromBase64String(secret.ProtectedKey); encrypted[^1] ^= 1;
secret.ProtectedKey = Convert.ToBase64String(encrypted);
Reject(() => protector.Unprotect(secret), "ciphertext tamper");
settings.EncryptionKeyBase64 = "";
Reject(() => protector.Protect(secret, plain), "missing encryption key fails closed");

// 只构造 EF 模型和 migration，不执行任何数据库操作。
using var db = new AuthDbContextDesignFactory().CreateDbContext([]);
void Unique(Type entity, params string[] properties) => Check(db.Model.FindEntityType(entity)!.GetIndexes().Any(
    x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(properties)), "unique " + entity.Name);
Unique(typeof(ManufacturerSecret), "VendorId", "ManufacturerId");
Unique(typeof(ProductionRegistration), "VendorId", "OperationId");
Unique(typeof(DongleEntitlement), "VendorId", "ActivationSourceHardwareKeyId");
Unique(typeof(ProductionRegistration), "EntitlementId", "ProductionRevision");
Check(db.Model.FindEntityType(typeof(DongleEntitlement))!.FindProperty("ProductionRevision")!.IsConcurrencyToken,
    "production head participates in optimistic concurrency");
Check(db.Model.FindEntityType(typeof(ProductionRegistration))!.FindProperty("ProductionRevision")!.IsNullable,
    "unknown stored revision is not invented by migration");
Check(db.Model.FindEntityType(typeof(ProductionRegistration))!.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.Restrict,
    "entitlement cannot be deleted through registrations");
var assembly = db.GetService<IMigrationsAssembly>();
var migrationType = assembly.Migrations.Single(x => x.Key.EndsWith("_AddSuperDogProduction")).Value;
var migration = assembly.CreateMigration(migrationType, db.Database.ProviderName!);
Check(migration.UpOperations.Count(x => x is CreateTableOperation) == 3, "three new tables");
Check(migration.UpOperations.All(x => x is CreateTableOperation or CreateIndexOperation), "migration has no unrelated or destructive operation");
Check(migration.DownOperations.OfType<DropTableOperation>().Count() == 3, "reversible migration");
var sql = db.GetService<IMigrator>().GenerateScript("20260805120000_PersistPlainTextApiKeys", assembly.Migrations.Single(x => x.Key.EndsWith("_AddSuperDogProduction")).Key);
Check(sql.Contains("CREATE TABLE `ManufacturerKeys`") && sql.Contains("bigint unsigned") && sql.Contains("utf8mb4_bin"), "MySQL migration SQL generation");
var revisionMigrationEntry = assembly.Migrations.Single(x => x.Key.EndsWith("_TrackSuperDogProductionRevision"));
var revisionMigration = assembly.CreateMigration(revisionMigrationEntry.Value, db.Database.ProviderName!);
var addedRevisionColumns = revisionMigration.UpOperations.OfType<AddColumnOperation>().ToArray();
Check(addedRevisionColumns.Length == 2 && addedRevisionColumns.All(x => x.Name == "ProductionRevision"),
    "production revision migration only adds the two production columns");
Check(addedRevisionColumns.Single(x => x.Table == "ProductionRegistrations").IsNullable &&
      addedRevisionColumns.Single(x => x.Table == "DongleEntitlements").DefaultValue is 0,
    "migration keeps unknown history and head unversioned instead of inventing order");
Check(revisionMigration.UpOperations.OfType<CreateIndexOperation>().Single() is { IsUnique: true } revisionIndex &&
      revisionIndex.Columns.SequenceEqual(new[] { "EntitlementId", "ProductionRevision" }),
    "database prevents concurrent different operations claiming one hardware revision");
Check(revisionMigration.UpOperations.All(x => x is AddColumnOperation or CreateIndexOperation or DropIndexOperation) &&
      revisionMigration.DownOperations.OfType<DropColumnOperation>().Count() == 2,
    "production revision migration is additive and reversible");
var revisionSql = db.GetService<IMigrator>().GenerateScript(migrationType.GetCustomAttribute<MigrationAttribute>()!.Id,
    revisionMigrationEntry.Key);
Check(revisionSql.Contains("ADD `ProductionRevision` int") && revisionSql.Contains("CREATE UNIQUE INDEX") &&
      !revisionSql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase), "MySQL production revision migration SQL");

var controller = typeof(DongleLicensesController);
Check(controller.GetCustomAttribute<AuthorizeAttribute>()!.Policy == "SuperDogProduction", "controller authenticated policy");
string[] Required(string method) => controller.GetMethod(method)!.GetCustomAttributes<PermissionAttribute>().Select(x => x.Permission).ToArray();
Check(Required("Copy").Order().SequenceEqual(new[] { "superdog.keys.copy", "superdog.keys.view" }), "copy needs both permissions");
Check(Required("Retrieve").SequenceEqual(["superdog.keys.view"]), "retrieve permission");
Check(Required("Ensure").SequenceEqual(["superdog.keys.manage"]), "ensure management only");
Check(Required("Upload").SequenceEqual(["superdog.records.upload"]), "upload permission");
Check(controller.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore, "KEY response caching disabled");
Check(SuperDogOptions.ClientId == "superdog41", "exact client ID");
var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "../../../../../Lumine.AuthServer/appsettings.json"))).Build();
var seeds = config.GetSection("SeedData").Get<AuthSeedOptions>()!;
Check(seeds.Clients.Any(x => x.ClientId == "superdog41" && !x.RequirePkce && x.ClientName == "SuperDog4.1" &&
    x.AllowedScopes.SequenceEqual(new[] { "openid", "profile", "roles", "permissions" })), "configured client registration");
foreach (string p in new[] { "superdog.read", "superdog.write", "superdog.rewrite", "superdog.records.view", "superdog.records.manage",
    "superdog.records.export", "superdog.records.upload", "superdog.audit.view", "superdog.audit.export", "superdog.settings.manage",
    "dongle.activation.manage", "superdog.keys.view", "superdog.keys.copy", "superdog.keys.manage" })
    Check(seeds.Permissions.Any(x => x.Name == p), "seed permission " + p);
Check(!seeds.Permissions.Any(x => x.Name is "superdog.production.upload" or "superdog.audit.read"), "no obsolete permission IDs");
Console.WriteLine($"PASS {count} offline assertions (no database/network/environment secrets).");
