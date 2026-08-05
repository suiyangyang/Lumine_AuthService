using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Application.Services;
using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationAppService _authenticationAppService;
        private readonly IUserRepository _userRepository;

        public AuthController(
            IAuthenticationAppService authenticationAppService,
            IUserRepository userRepository)
        {
            _authenticationAppService = authenticationAppService;
            _userRepository = userRepository;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _authenticationAppService.LoginAsync(
                    new LoginInput(req.UserName, req.Password, req.Scope, req.ClientId, req.Nonce),
                    cancellationToken);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    CreateSessionPrincipal(result),
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn)
                    });

                return Ok(new
                {
                    token = result.AccessToken,
                    access_token = result.AccessToken,
                    token_type = "Bearer",
                    expires_in = result.ExpiresIn,
                    scope = result.Scope,
                    id_token = result.IdToken,
                    user = new
                    {
                        result.User.Id,
                        result.User.UserName,
                        result.User.Email,
                        Roles = result.Roles.Select(role => role.Name).ToList(),
                        Permissions = result.Permissions
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = "invalid_request", message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("invalid_scope::", StringComparison.Ordinal))
            {
                return BadRequest(new { error = "invalid_scope", message = ex.Message[15..] });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("invalid_client::", StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_client", message = ex.Message[16..] });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("invalid_request::", StringComparison.Ordinal))
            {
                return BadRequest(new { error = "invalid_request", message = ex.Message[17..] });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("conflict::", StringComparison.Ordinal))
            {
                return Conflict(new { error = "conflict", message = ex.Message[10..] });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = "invalid_grant", message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _authenticationAppService.RegisterAsync(
                    new RegisterInput(request.UserName, request.Email, request.Password, request.NickName, request.PhoneNumber),
                    cancellationToken);

                return Created($"/api/users/{result.User.Id}", new
                {
                    result.User.Id,
                    result.User.UserName,
                    result.User.Email,
                    result.User.NickName,
                    result.User.PhoneNumber,
                    result.User.IsActive
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("invalid_request::", StringComparison.Ordinal))
            {
                return BadRequest(new { error = "invalid_request", message = ex.Message[17..] });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("conflict::", StringComparison.Ordinal))
            {
                return Conflict(new { error = "conflict", message = ex.Message[10..] });
            }
        }

        [AllowAnonymous]
        [HttpPost("api-key/token")]
        public async Task<IActionResult> ApiKeyToken([FromBody] ApiKeyTokenRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _authenticationAppService.LoginWithApiKeyAsync(
                    new ApiKeyLoginInput(request.ApiKey, request.ClientId, request.Scope, request.Nonce),
                    cancellationToken);

                return Ok(new
                {
                    token = result.AccessToken,
                    access_token = result.AccessToken,
                    token_type = "Bearer",
                    expires_in = result.ExpiresIn,
                    scope = result.Scope,
                    id_token = result.IdToken,
                    user = new
                    {
                        result.User.Id,
                        result.User.UserName,
                        result.User.Email,
                        Roles = result.Roles.Select(role => role.Name).ToList(),
                        Permissions = result.Permissions
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = "invalid_request", message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("invalid_scope::", StringComparison.Ordinal))
            {
                return BadRequest(new { error = "invalid_scope", message = ex.Message[15..] });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("invalid_client::", StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "invalid_client", message = ex.Message[16..] });
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("invalid_request::", StringComparison.Ordinal))
            {
                return BadRequest(new { error = "invalid_request", message = ex.Message[17..] });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = "invalid_grant", message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("me")]
        public Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
        {
            return BuildCurrentUserResponse(cancellationToken);
        }

        [Authorize]
        [HttpGet("me/permissions")]
        public Task<IActionResult> GetCurrentUserPermissions(CancellationToken cancellationToken)
        {
            return BuildCurrentUserResponse(cancellationToken);
        }

        [Authorize]
        [HttpPost("check-permission")]
        public async Task<IActionResult> CheckPermission([FromBody] CheckPermissionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PermissionName))
            {
                return BadRequest(new { error = "invalid_request", message = "permissionName 不能为空。" });
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { error = "invalid_token", message = "Token 中缺少有效的用户标识。" });
            }

            var user = await _userRepository.GetByIdAsync(currentUserId.Value);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { error = "invalid_token", message = "当前用户不存在或已停用。" });
            }

            var permissionName = request.PermissionName.Trim();
            var allowed = await _userRepository.UserHasPermissionAsync(currentUserId.Value, permissionName);
            return Ok(new
            {
                userId = currentUserId.Value,
                permissionName,
                allowed
            });
        }

        [Authorize(AuthenticationSchemes = "Bearer,Cookies")]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var userName = User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirst("preferred_username")?.Value
                ?? User.Identity?.Name
                ?? "未知用户";
            var clientId = User.FindFirst("aud")?.Value ?? "后台登录";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _authenticationAppService.LogoutAsync(
                new LogoutInput(userName, clientId, ipAddress),
                cancellationToken);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok(new { message = "已记录登出日志。" });
        }

        private async Task<IActionResult> BuildCurrentUserResponse(CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { error = "invalid_token", message = "Token 中缺少有效的用户标识。" });
            }

            var user = await _userRepository.GetByIdAsync(currentUserId.Value);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { error = "invalid_token", message = "当前用户不存在或已停用。" });
            }

            var roles = await _userRepository.GetUserRolesWithPermissionsAsync(user.Id);
            var roleDtos = roles
                .OrderBy(role => role.Name)
                .Select(role => new CurrentUserRoleDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    Permissions = role.RolePermissions
                        .Select(item => item.PermissionName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(permission => permission)
                        .ToArray()
                })
                .ToArray();

            var permissions = roleDtos
                .SelectMany(role => role.Permissions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(permission => permission)
                .ToArray();

            return Ok(new CurrentUserAccessDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                NickName = user.NickName,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                Roles = roleDtos,
                Permissions = permissions
            });
        }

        private Guid? GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            return Guid.TryParse(userIdValue, out var userId) ? userId : null;
        }

        private static ClaimsPrincipal CreateSessionPrincipal(AuthenticationResult result)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
                new("sub", result.User.Id.ToString()),
                new(ClaimTypes.Name, result.User.UserName),
                new("username", result.User.UserName)
            };

            claims.AddRange(result.Roles.Select(role => new Claim(ClaimTypes.Role, role.Name)));
            claims.AddRange(result.Permissions.Select(permission => new Claim("permission", permission)));

            return new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme));
        }
    }

    public record LoginRequest(string UserName, string Password, string? Scope = null, string? ClientId = null, string? Nonce = null);

    public record ApiKeyTokenRequest(string ApiKey, string ClientId, string? Scope = null, string? Nonce = null);

    public record RegisterRequest(string UserName, string Email, string Password, string? NickName = null, string? PhoneNumber = null);

    public record CheckPermissionRequest(string PermissionName);
}
