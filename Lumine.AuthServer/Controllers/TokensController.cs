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
    public class TokensController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;

        public TokensController(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        [Permission("clients.view")]
        public async Task<ActionResult<PagedResultDto<RefreshTokenRecordDto>>> GetAll(
            [FromQuery] string? keyword,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

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
