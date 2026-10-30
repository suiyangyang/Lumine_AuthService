using System.Data.Common;
using System.Security.Claims;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Api.Auth;

// 放在现有认证之后、授权之前：记录权限中间件拒绝，不改变其授权结果。
public sealed class SuperDogRejectionAuditMiddleware(RequestDelegate next, ILogger<SuperDogRejectionAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAuditLogService audit)
    {
        if (!context.Request.Path.StartsWithSegments("/api/dongle-licenses"))
        {
            await next(context);
            return;
        }
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        await next(context);
        if (context.Response.StatusCode is 401 or 403)
        {
            var subject = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub") ?? "anonymous"
                : "anonymous";
            try
            {
                await audit.WriteAsync("SuperDog", "生产接口鉴权", subject, "production_api", "拒绝",
                    context.Response.StatusCode == 401 ? "unauthorized" : "forbidden",
                    cancellationToken: context.RequestAborted);
            }
            catch (Exception e) when (e is DbException or DbUpdateException)
            {
                // 不附异常对象或请求材料，避免驱动日志意外包含数据；拒绝结果保持原值。
                logger.LogWarning("SuperDog rejection audit could not be persisted.");
            }
        }
    }
}
