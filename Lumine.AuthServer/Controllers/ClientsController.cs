using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
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
    public class ClientsController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;
        private readonly IOidcClientRepository _clientRepository;

        public ClientsController(AuthDbContext dbContext, IOidcClientRepository clientRepository)
        {
            _dbContext = dbContext;
            _clientRepository = clientRepository;
        }

        [HttpGet]
        [Permission("clients.view")]
        public async Task<ActionResult<PagedResultDto<OidcClientDto>>> GetAll([FromQuery] string? keyword, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = ManagementApiDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? ManagementApiDefaults.DefaultPageSize : Math.Min(pageSize, ManagementApiDefaults.MaxPageSize);

            var query = _dbContext.OidcClients
                .AsNoTracking()
                .Include(item => item.RedirectUris)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(item => item.ClientId.Contains(normalizedKeyword) || item.ClientName.Contains(normalizedKeyword));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(item => item.ClientId)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResultDto<OidcClientDto>
            {
                Items = items.Select(MapClient).ToArray(),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }

        [HttpGet("{id:guid}")]
        [Permission("clients.view")]
        public async Task<ActionResult<OidcClientDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
            return client == null ? NotFound() : Ok(MapClient(client));
        }

        [HttpPost]
        [Permission("clients.manage")]
        public async Task<ActionResult<OidcClientDto>> Create([FromBody] SaveClientRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var normalizedScopes = NormalizeScopes(request.AllowedScopes);
                var existingClient = await _clientRepository.GetByClientIdAsync(request.ClientId.Trim(), cancellationToken);
                if (existingClient != null)
                {
                    return Conflict($"客户端 '{request.ClientId}' 已存在。");
                }

                var client = new OidcClient(Guid.NewGuid(), request.ClientId, request.ClientName, request.ClientType, normalizedScopes, request.RequirePkce, request.IsActive, request.Description);
                client.ReplaceRedirectUris(request.RedirectUris);

                await _clientRepository.AddAsync(client, cancellationToken);
                await _clientRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

                var createdClient = await _clientRepository.GetByIdAsync(client.Id, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = client.Id }, MapClient(createdClient!));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:guid}")]
        [Permission("clients.manage")]
        public async Task<ActionResult<OidcClientDto>> Update(Guid id, [FromBody] SaveClientRequest request, CancellationToken cancellationToken)
        {
            var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
            if (client == null)
            {
                return NotFound();
            }

            try
            {
                var existingClient = await _clientRepository.GetByClientIdAsync(request.ClientId.Trim(), cancellationToken);
                if (existingClient != null && existingClient.Id != id)
                {
                    return Conflict($"客户端 '{request.ClientId}' 已存在。");
                }

                var normalizedScopes = NormalizeScopes(request.AllowedScopes);
                client.Update(request.ClientId, request.ClientName, request.ClientType, normalizedScopes, request.RequirePkce, request.IsActive, request.Description);
                client.ReplaceRedirectUris(request.RedirectUris);
                _clientRepository.Update(client);
                await _clientRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

                var updatedClient = await _clientRepository.GetByIdAsync(id, cancellationToken);
                return Ok(MapClient(updatedClient!));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:guid}")]
        [Permission("clients.manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var client = await _clientRepository.GetByIdAsync(id, cancellationToken);
            if (client == null)
            {
                return NotFound();
            }

            _clientRepository.Remove(client);
            await _clientRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private static IReadOnlyCollection<string> NormalizeScopes(IEnumerable<string>? scopes)
        {
            var requestedScopes = scopes?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            var unsupportedScopes = requestedScopes
                .Where(item => !OidcScopes.SupportedScopes.Contains(item, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (unsupportedScopes.Length > 0)
            {
                throw new ArgumentException($"存在不支持的 scope: {string.Join(", ", unsupportedScopes)}");
            }

            return requestedScopes;
        }

        private static OidcClientDto MapClient(OidcClient client)
        {
            return new OidcClientDto
            {
                Id = client.Id,
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                ClientType = client.ClientType,
                AllowedScopes = client.ScopeList,
                RedirectUris = client.RedirectUris.Select(item => item.RedirectUri).OrderBy(item => item).ToArray(),
                RequirePkce = client.RequirePkce,
                IsActive = client.IsActive,
                Description = client.Description
            };
        }
    }

    public record SaveClientRequest(
        string ClientId,
        string ClientName,
        string ClientType,
        List<string> AllowedScopes,
        List<string> RedirectUris,
        bool RequirePkce,
        bool IsActive,
        string? Description);
}
