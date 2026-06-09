using Lumine.AuthServer.Application.Services;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationAppService _authenticationAppService;

        public AuthController(IAuthenticationAppService authenticationAppService)
        {
            _authenticationAppService = authenticationAppService;
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

        [Authorize]
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

            return Ok(new { message = "已记录登出日志。" });
        }
    }

    public record LoginRequest(string UserName, string Password, string? Scope = null, string? ClientId = null, string? Nonce = null);

    public record RegisterRequest(string UserName, string Email, string Password, string? NickName = null, string? PhoneNumber = null);
}
