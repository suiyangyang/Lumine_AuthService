using System.Security.Claims;
using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.DTOs.SuperDog;
using Lumine.AuthServer.Infrastructure;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace Lumine.AuthServer.Controllers;

[ApiController]
[Route("api/dongle-licenses")]
[Authorize(Policy = "SuperDogProduction")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(2_097_152)]
[EnableRateLimiting("SuperDogProduction")]
public sealed class DongleLicensesController(SuperDogProductionService production, AuthDbContext db) : Controller
{
    private ProductionActor Actor => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!,
        User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("preferred_username") ?? "");

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(Actor.Subject, out var id) ||
            !await db.Users.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, context.HttpContext.RequestAborted))
        {
            context.Result = Unauthorized(new { error = "invalid_token" });
            return;
        }
        var executed = await next();
        if (executed.Exception is SuperDogUnavailableException)
        {
            db.ChangeTracker.Clear();
            await AuditRejection("configuration_unavailable", context.HttpContext.RequestAborted);
            executed.ExceptionHandled = true;
            executed.Result = StatusCode(503, new { error = "configuration_unavailable" });
        }
        else if (executed.Exception is InvalidDataException)
        {
            db.ChangeTracker.Clear();
            await AuditRejection("invalid_request", context.HttpContext.RequestAborted);
            executed.ExceptionHandled = true;
            executed.Result = BadRequest(new { error = "invalid_request" });
        }
    }

    private async Task AuditRejection(string code, CancellationToken ct)
    {
        db.AuditLogEntries.Add(new Lumine.AuthServer.Domain.Entities.AuditLogEntry(Guid.NewGuid(), "SuperDog",
            "厂家KEY操作", Actor.Subject, "manufacturer_key", "拒绝", code, null, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    [HttpPost("manufacturer-keys/ensure")]
    [Permission("superdog.keys.manage")]
    public async Task<ActionResult<ManufacturerKeyInfo>> Ensure(ManufacturerKeyRequest request, CancellationToken ct) =>
        Ok(await production.EnsureKeyAsync(request.ManufacturerId, Actor, ct));

    [HttpPost("manufacturer-keys/retrieve")]
    [Permission("superdog.keys.view")]
    public Task<ActionResult<ManufacturerKey>> Retrieve(ManufacturerKeyRequest request, CancellationToken ct) => GetKey(request, false, ct);

    [HttpPost("manufacturer-keys/copy")]
    [Permission("superdog.keys.view")]
    [Permission("superdog.keys.copy")]
    public Task<ActionResult<ManufacturerKey>> Copy(ManufacturerKeyRequest request, CancellationToken ct) => GetKey(request, true, ct);

    private async Task<ActionResult<ManufacturerKey>> GetKey(ManufacturerKeyRequest request, bool copy, CancellationToken ct)
    {
        var key = await production.RetrieveKeyAsync(request.ManufacturerId, copy, Actor, ct);
        return key == null ? NotFound(new { error = "key_unavailable" }) : Ok(key);
    }

    [HttpPost("production-records/batch")]
    [Permission("superdog.records.upload")]
    public async Task<ActionResult<ProductionBatchResponse>> Upload(ProductionBatchRequest request, CancellationToken ct)
    {
        if (request.Records == null || request.Records.Count is < 1 or > 100)
            return BadRequest(new { error = "invalid_batch", message = "每批须包含1—100条记录。" });
        var results = new List<UploadResult>(request.Records.Count);
        foreach (var record in request.Records) results.Add(await production.UploadAsync(record, Actor, ct));
        return Ok(new ProductionBatchResponse(results));
    }
}
