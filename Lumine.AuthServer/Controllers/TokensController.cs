using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Infrastructure;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TokensController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;
        private readonly IAuditLogService _auditLogService;

        public TokensController(AuthDbContext dbContext, IAuditLogService auditLogService)
        {
            _dbContext = dbContext;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        [Permission("clients.view")]
        public async Task<ActionResult<PagedResultDto<RefreshTokenRecordDto>>> GetAll(
            [FromQuery] string? keyword,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = ManagementApiDefaults.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? ManagementApiDefaults.DefaultPageSize : Math.Min(pageSize, ManagementApiDefaults.MaxPageSize);

            var query = _dbContext.RefreshTokens
                .AsNoTracking()
                .Include(item => item.User)
                .Include(item => item.Client)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(item =>
                    item.Token.Contains(normalizedKeyword)
                    || (item.User != null && (item.User.UserName.Contains(normalizedKeyword) || item.User.Email.Contains(normalizedKeyword)))
                    || (item.Client != null && (item.Client.ClientId.Contains(normalizedKeyword) || item.Client.ClientName.Contains(normalizedKeyword))));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(item => item.CreatedAtUtc)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResultDto<RefreshTokenRecordDto>
            {
                Items = items.Select(MapToken).ToArray(),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }

        [HttpPost("{id:guid}/revoke")]
        [Permission("clients.manage")]
        public async Task<ActionResult<RefreshTokenRecordDto>> Revoke(Guid id, CancellationToken cancellationToken)
        {
            var token = await _dbContext.RefreshTokens
                .Include(item => item.User)
                .Include(item => item.Client)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (token == null)
            {
                return NotFound();
            }

            token.Revoke(DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "Token",
                "撤销",
                User.Identity?.Name ?? "当前用户",
                token.Client?.ClientName ?? token.Client?.ClientId ?? "未知客户端",
                "成功",
                $"Token: {MapToken(token).TokenPreview} · 用户: {token.User?.UserName ?? "未知用户"}",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                DateTime.UtcNow,
                cancellationToken);
            return Ok(MapToken(token));
        }

        private static RefreshTokenRecordDto MapToken(Domain.Entities.RefreshToken token)
        {
            var value = token.Token ?? string.Empty;
            var preview = value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";

            return new RefreshTokenRecordDto
            {
                Id = token.Id,
                TokenPreview = preview,
                UserId = token.UserId,
                UserName = token.User?.UserName ?? "未知用户",
                UserEmail = token.User?.Email,
                ClientId = token.Client?.ClientId ?? "未知客户端",
                ClientName = token.Client?.ClientName ?? "未知客户端",
                Scopes = token.ScopeList.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
                CreatedAtUtc = token.CreatedAtUtc,
                ExpiresAtUtc = token.ExpiresAtUtc,
                RevokedAtUtc = token.RevokedAtUtc
            };
        }
    }
}
