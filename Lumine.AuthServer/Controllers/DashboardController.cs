using System.Security.Claims;
using Lumine.AuthServer.Application.DTOs;
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

            var userCount = await CountIfAllowedAsync(canViewUsers, _dbContext.Users.AsNoTracking(), cancellationToken);
            var roleCount = await CountIfAllowedAsync(canViewRoles, _dbContext.Roles.AsNoTracking(), cancellationToken);
            var permissionCount = await CountIfAllowedAsync(canViewPermissions, _dbContext.Permissions.AsNoTracking(), cancellationToken);
            var clientCount = await CountIfAllowedAsync(canViewClients, _dbContext.OidcClients.AsNoTracking(), cancellationToken);

            var utcToday = DateTime.UtcNow.Date;
            var trendStartDate = utcToday.AddDays(-6);
            var recentLoginStartDate = utcToday.AddDays(-30);

            var loginTrendEntries = canViewLoginActivity
                ? await _dbContext.AuthorizationCodes
                    .AsNoTracking()
                    .Where(item => item.CreatedAtUtc >= trendStartDate)
                    .Select(item => item.CreatedAtUtc)
                    .ToListAsync(cancellationToken)
                : [];

            var tokenTrendEntries = canViewTokenActivity
                ? await _dbContext.RefreshTokens
                    .AsNoTracking()
                    .Where(item => item.CreatedAtUtc >= trendStartDate)
                    .Select(item => item.CreatedAtUtc)
                    .ToListAsync(cancellationToken)
                : [];

            var recentLoginUsers = canViewLoginActivity
                ? await _dbContext.AuthorizationCodes
                    .AsNoTracking()
                    .Where(item => item.CreatedAtUtc >= recentLoginStartDate)
                    .Include(item => item.User)
                    .Include(item => item.Client)
                    .OrderByDescending(item => item.CreatedAtUtc)
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
                GeneratedAtUtc = DateTime.UtcNow
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

        private static List<RecentLoginUserDto> BuildRecentLoginUsers(IEnumerable<Domain.Entities.AuthorizationCode> authorizationCodes, bool includeEmail)
        {
            return authorizationCodes
                .Where(item => item.User != null)
                .GroupBy(item => item.UserId)
                .Select(group => group.OrderByDescending(item => item.CreatedAtUtc).First())
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(8)
                .Select(item => new RecentLoginUserDto
                {
                    UserId = item.UserId,
                    UserName = item.User?.UserName ?? "未知用户",
                    Email = includeEmail ? item.User?.Email : null,
                    ClientId = item.Client?.ClientId,
                    ClientName = item.Client?.ClientName,
                    LoginAtUtc = item.CreatedAtUtc
                })
                .ToList();
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
