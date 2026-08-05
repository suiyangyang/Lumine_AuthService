using System.Security.Claims;
using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Domain.Entities;
using Lumine.AuthServer.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;

        public DashboardController(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
        {
            var permissionSet = User.Claims
                .Where(claim =>
                    string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase))
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var canViewUsers = HasAnyPermission(permissionSet, "users.view");
            var canViewRoles = HasAnyPermission(permissionSet, "roles.view");
            var canViewPermissions = HasAnyPermission(permissionSet, "permissions.view");
            var canViewClients = HasAnyPermission(permissionSet, "clients.view");
            var canViewLoginActivity = canViewUsers || canViewClients;
            var canViewTokenActivity = canViewClients;
            var utcNow = DateTime.UtcNow;

            var userCount = await CountIfAllowedAsync(canViewUsers, _dbContext.Users.AsNoTracking(), cancellationToken);
            var roleCount = await CountIfAllowedAsync(canViewRoles, _dbContext.Roles.AsNoTracking(), cancellationToken);
            var permissionCount = await CountIfAllowedAsync(canViewPermissions, _dbContext.Permissions.AsNoTracking(), cancellationToken);
            var clientCount = await CountIfAllowedAsync(canViewClients, _dbContext.OidcClients.AsNoTracking(), cancellationToken);

            var utcToday = utcNow.Date;
            var trendStartDate = utcToday.AddDays(-6);
            var recentLoginStartDate = utcToday.AddDays(-30);

            var loginTrendEntries = canViewLoginActivity
                ? await _dbContext.AuditLogEntries
                    .AsNoTracking()
                    .Where(item => item.Category == "认证"
                        && item.Action == "登录"
                        && item.Outcome == "成功"
                        && item.OccurredAtUtc >= trendStartDate)
                    .Select(item => item.OccurredAtUtc)
                    .ToListAsync(cancellationToken)
                : [];

            int? failedLogins24h = canViewLoginActivity
                ? await _dbContext.AuditLogEntries
                    .AsNoTracking()
                    .Where(item => item.Category == "认证"
                        && item.Action == "登录"
                        && item.Outcome == "失败"
                        && item.OccurredAtUtc >= utcNow.AddHours(-24))
                    .CountAsync(cancellationToken)
                : null;

            int? expiringRefreshTokens7d = canViewTokenActivity
                ? await _dbContext.RefreshTokens
                    .AsNoTracking()
                    .Where(item => item.RevokedAtUtc == null
                        && item.ExpiresAtUtc > utcNow
                        && item.ExpiresAtUtc <= utcNow.AddDays(7))
                    .CountAsync(cancellationToken)
                : null;

            int? clientConfigurationWarnings = canViewClients
                ? await _dbContext.OidcClients
                    .AsNoTracking()
                    .Where(item => item.IsActive
                        && !string.Equals(item.ClientId, ApiKeyClientIds.LabChromKnowledge, StringComparison.OrdinalIgnoreCase)
                        && (!item.RedirectUris.Any() || item.AllowedScopes == string.Empty))
                    .CountAsync(cancellationToken)
                : null;

            int? reviewableAuditEvents24h = canViewClients
                ? await _dbContext.AuditLogEntries
                    .AsNoTracking()
                    .Where(item => item.Outcome == "失败"
                        && item.OccurredAtUtc >= utcNow.AddHours(-24)
                        && !(item.Category == "认证" && item.Action == "登录"))
                    .CountAsync(cancellationToken)
                : null;

            var tokenTrendEntries = canViewTokenActivity
                ? await _dbContext.RefreshTokens
                    .AsNoTracking()
                    .Where(item => item.CreatedAtUtc >= trendStartDate)
                    .Select(item => item.CreatedAtUtc)
                    .ToListAsync(cancellationToken)
                : [];

            var recentLoginUsers = canViewLoginActivity
                ? await _dbContext.Users
                    .AsNoTracking()
                    .Where(item => item.LastLoginAtUtc.HasValue && item.LastLoginAtUtc >= recentLoginStartDate)
                    .OrderByDescending(item => item.LastLoginAtUtc)
                    .Take(100)
                    .ToListAsync(cancellationToken)
                : [];

            var summary = new DashboardSummaryDto
            {
                Users = CreateMetric("用户总数", userCount, canViewUsers, "基于当前用户清单统计。"),
                Roles = CreateMetric("角色数量", roleCount, canViewRoles, "统计系统内角色定义数量。"),
                Permissions = CreateMetric("权限数量", permissionCount, canViewPermissions, "统计系统内权限点数量。"),
                Clients = CreateMetric("OAuth 客户端数量", clientCount, canViewClients, "统计已注册客户端数量。"),
                LoginTrend = BuildTrend(loginTrendEntries, trendStartDate, utcToday),
                TokenIssueTrend = BuildTrend(tokenTrendEntries, trendStartDate, utcToday),
                RecentLoginUsers = BuildRecentLoginUsers(recentLoginUsers, canViewUsers),
                OperationalSignals = new DashboardOperationalSignalsDto
                {
                    FailedLogins24h = CreateOperationalSignal(
                        "failed-logins-24h",
                        "登录失败",
                        failedLogins24h,
                        canViewLoginActivity,
                        "过去 24 小时认证失败次数。"),
                    ExpiringRefreshTokens7d = CreateOperationalSignal(
                        "expiring-refresh-tokens-7d",
                        "即将过期 Token",
                        expiringRefreshTokens7d,
                        canViewTokenActivity,
                        "未来 7 天内到期且尚未撤销的 Refresh Token 数量。"),
                    ClientConfigurationWarnings = CreateOperationalSignal(
                        "client-configuration-warnings",
                        "客户端配置异常",
                        clientConfigurationWarnings,
                        canViewClients,
                        "启用客户端中缺少 Redirect URI 或允许 Scope 的客户端数量。"),
                    ReviewableAuditEvents24h = CreateOperationalSignal(
                        "reviewable-audit-events-24h",
                        "需复核审计事件",
                        reviewableAuditEvents24h,
                        canViewClients,
                        "过去 24 小时除登录失败外的失败审计事件数量，作为待复核代理。")
                },
                GeneratedAtUtc = utcNow
            };

            return Ok(summary);
        }

        private static DashboardMetricDto CreateMetric(string title, int? value, bool isVisible, string description)
        {
            return new DashboardMetricDto
            {
                Title = title,
                Value = value,
                IsVisible = isVisible,
                Description = isVisible ? description : "当前账号缺少对应查看权限。"
            };
        }

        private static List<DashboardTrendPointDto> BuildTrend(IEnumerable<DateTime> entries, DateTime startDate, DateTime endDate)
        {
            var buckets = entries
                .GroupBy(item => item.Date)
                .ToDictionary(group => group.Key, group => group.Count());

            var result = new List<DashboardTrendPointDto>();
            for (var cursor = startDate.Date; cursor <= endDate.Date; cursor = cursor.AddDays(1))
            {
                result.Add(new DashboardTrendPointDto
                {
                    Date = cursor,
                    Value = buckets.GetValueOrDefault(cursor, 0)
                });
            }

            return result;
        }

        private static List<RecentLoginUserDto> BuildRecentLoginUsers(IEnumerable<Domain.Entities.User> users, bool includeEmail)
        {
            return users
                .Take(8)
                .Select(item => new RecentLoginUserDto
                {
                    UserId = item.Id,
                    UserName = item.UserName,
                    Email = includeEmail ? item.Email : null,
                    LoginAtUtc = item.LastLoginAtUtc!.Value
                })
                .ToList();
        }

        private static DashboardOperationalSignalDto CreateOperationalSignal(
            string key,
            string label,
            int? value,
            bool isVisible,
            string description)
        {
            return new DashboardOperationalSignalDto
            {
                Key = key,
                Label = label,
                Value = value,
                IsVisible = isVisible,
                Description = isVisible ? description : "当前账号缺少对应查看权限。"
            };
        }

        private static bool HasAnyPermission(IReadOnlySet<string> permissionSet, params string[] requiredPermissions)
        {
            return requiredPermissions.Any(permission => permissionSet.Contains(permission));
        }

        private static async Task<int?> CountIfAllowedAsync<TEntity>(bool isAllowed, IQueryable<TEntity> query, CancellationToken cancellationToken)
            where TEntity : class
        {
            if (!isAllowed)
            {
                return null;
            }

            return await query.CountAsync(cancellationToken);
        }
    }
}
