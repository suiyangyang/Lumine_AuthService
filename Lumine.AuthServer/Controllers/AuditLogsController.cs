using Lumine.AuthServer.Api.Auth;
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
    public class AuditLogsController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;

        public AuditLogsController(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        [Permission("clients.view")]
        public async Task<ActionResult<PagedResultDto<AuditLogEntryDto>>> GetAll(
            [FromQuery] string? keyword,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var authorizationCodes = await _dbContext.AuthorizationCodes
                .AsNoTracking()
                .Include(item => item.User)
                .Include(item => item.Client)
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            var refreshTokens = await _dbContext.RefreshTokens
                .AsNoTracking()
                .Include(item => item.User)
                .Include(item => item.Client)
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            var entries = authorizationCodes
                .Select(item => new AuditLogEntryDto
                {
                    Id = $"auth-code:{item.Id}",
                    Category = "授权码",
                    Action = item.IsConsumed ? "签发并消费" : "签发",
                    Actor = item.User?.UserName ?? "未知用户",
                    Target = item.Client?.ClientName ?? item.Client?.ClientId ?? "未知客户端",
                    Outcome = item.IsExpired(DateTime.UtcNow) ? "已过期" : "成功",
                    Details = $"Scopes: {string.Join(' ', item.ScopeList)}",
                    OccurredAtUtc = item.CreatedAtUtc
                })
                .Concat(refreshTokens.Select(item => new AuditLogEntryDto
                {
                    Id = $"refresh-token:{item.Id}",
                    Category = "Refresh Token",
                    Action = item.RevokedAtUtc.HasValue ? "撤销" : "签发",
                    Actor = item.User?.UserName ?? "未知用户",
                    Target = item.Client?.ClientName ?? item.Client?.ClientId ?? "未知客户端",
                    Outcome = item.RevokedAtUtc.HasValue ? "已撤销" : item.ExpiresAtUtc <= DateTime.UtcNow ? "已过期" : "有效",
                    Details = $"Scopes: {string.Join(' ', item.ScopeList)}",
                    OccurredAtUtc = item.RevokedAtUtc ?? item.CreatedAtUtc
                }))
                .OrderByDescending(item => item.OccurredAtUtc)
                .ToList();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                entries = entries.Where(item =>
                        item.Category.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.Action.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.Actor.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.Target.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.Outcome.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.Details.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var totalCount = entries.Count;
            var pagedItems = entries
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return Ok(new PagedResultDto<AuditLogEntryDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }
    }
}
