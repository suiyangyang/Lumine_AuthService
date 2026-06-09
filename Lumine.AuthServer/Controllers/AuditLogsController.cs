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
            [FromQuery] int pageSize = ManagementApiDefaults.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? ManagementApiDefaults.DefaultPageSize : Math.Min(pageSize, ManagementApiDefaults.MaxPageSize);

            var persistedQuery = _dbContext.AuditLogEntries
                .AsNoTracking()
                .Select(item => new AuditLogEntryDto
                {
                    Id = $"audit:{item.Id}",
                    Category = item.Category,
                    Action = item.Action,
                    Actor = item.Actor,
                    Target = item.Target,
                    Outcome = item.Outcome,
                    Details = string.IsNullOrWhiteSpace(item.IpAddress)
                        ? item.Details
                        : $"{item.Details} · IP: {item.IpAddress}",
                    OccurredAtUtc = item.OccurredAtUtc
                });

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                persistedQuery = persistedQuery.Where(item =>
                    item.Category.Contains(normalizedKeyword)
                    || item.Action.Contains(normalizedKeyword)
                    || item.Actor.Contains(normalizedKeyword)
                    || item.Target.Contains(normalizedKeyword)
                    || item.Outcome.Contains(normalizedKeyword)
                    || item.Details.Contains(normalizedKeyword));
            }

            var totalCount = await persistedQuery.CountAsync(cancellationToken);
            var pagedItems = await persistedQuery
                .OrderByDescending(item => item.OccurredAtUtc)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToArrayAsync(cancellationToken);

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
