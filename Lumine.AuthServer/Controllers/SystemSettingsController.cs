using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Infrastructure;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SystemSettingsController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;
        private readonly IOidcSigningCredentialsService _oidcSigningCredentialsService;
        private readonly AuthSeedOptions _seedOptions;

        public SystemSettingsController(
            AuthDbContext dbContext,
            IOidcSigningCredentialsService oidcSigningCredentialsService,
            IOptions<AuthSeedOptions> seedOptions)
        {
            _dbContext = dbContext;
            _oidcSigningCredentialsService = oidcSigningCredentialsService;
            _seedOptions = seedOptions.Value;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<SystemSettingsSummaryDto>> GetSummary(CancellationToken cancellationToken = default)
        {
            var userCount = await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
            var roleCount = await _dbContext.Roles.AsNoTracking().CountAsync(cancellationToken);
            var permissionCount = await _dbContext.Permissions.AsNoTracking().CountAsync(cancellationToken);
            var clientCount = await _dbContext.OidcClients.AsNoTracking().CountAsync(cancellationToken);

            var issuer = _oidcSigningCredentialsService.Issuer.TrimEnd('/');
            return Ok(new SystemSettingsSummaryDto
            {
                Issuer = issuer,
                AuthorizationEndpoint = $"{issuer}/connect/authorize",
                TokenEndpoint = $"{issuer}/connect/token",
                RefreshTokenGrantAdvertised = true,
                SeedEnabled = _seedOptions.Enabled,
                UserCount = userCount,
                RoleCount = roleCount,
                PermissionCount = permissionCount,
                ClientCount = clientCount,
                DefaultClientId = _seedOptions.Clients.FirstOrDefault()?.ClientId ?? string.Empty,
                ServerTimeUtc = DateTime.UtcNow
            });
        }
    }
}
