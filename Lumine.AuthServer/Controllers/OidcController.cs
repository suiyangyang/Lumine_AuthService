using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Route("connect")]
    public class OidcController : ControllerBase
    {
        private static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
        private readonly IUserRepository _userRepository;
        private readonly IOidcClientRepository _clientRepository;
        private readonly IAuthorizationCodeRepository _authorizationCodeRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IOidcService _oidcService;
        private readonly IOidcSigningCredentialsService _signingCredentialsService;
        private readonly IAuditLogService _auditLogService;

        public OidcController(
            IUserRepository userRepository,
            IOidcClientRepository clientRepository,
            IAuthorizationCodeRepository authorizationCodeRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IOidcService oidcService,
            IOidcSigningCredentialsService signingCredentialsService,
            IAuditLogService auditLogService)
        {
            _userRepository = userRepository;
            _clientRepository = clientRepository;
            _authorizationCodeRepository = authorizationCodeRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _oidcService = oidcService;
            _signingCredentialsService = signingCredentialsService;
            _auditLogService = auditLogService;
        }

        [AllowAnonymous]
        [HttpGet("authorize")]
        public async Task<IActionResult> Authorize([FromQuery] AuthorizeRequest request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.ResponseType, "code", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "unsupported_response_type", message = "当前仅支持 response_type=code。" });
            }

            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.RedirectUri))
            {
                return BadRequest(new { error = "invalid_request", message = "client_id 与 redirect_uri 不能为空。" });
            }

            var client = await _clientRepository.GetByClientIdAsync(request.ClientId.Trim(), cancellationToken);
            if (client == null || !client.IsActive)
            {
                return BadRequest(new { error = "invalid_client", message = "客户端不存在或已停用。" });
            }

            if (!client.HasRedirectUri(request.RedirectUri.Trim()))
            {
                return BadRequest(new { error = "invalid_request", message = "redirect_uri 不在客户端白名单中。" });
            }

            IReadOnlyCollection<string> scopes;
            try
            {
                scopes = _oidcService.NormalizeScopes(request.Scope);
            }
            catch (ArgumentException ex)
            {
                return CreateRedirectError(client, request.RedirectUri, request.State, "invalid_scope", ex.Message);
            }

            if (!client.AllowsScopes(scopes))
            {
                return CreateRedirectError(client, request.RedirectUri, request.State, "invalid_scope", "客户端未被授予所请求的 scope。");
            }

            if (client.RequirePkce)
            {
                if (string.IsNullOrWhiteSpace(request.CodeChallenge))
                {
                    return CreateRedirectError(client, request.RedirectUri, request.State, "invalid_request", "当前客户端必须提供 code_challenge。");
                }

                if (!string.Equals(request.CodeChallengeMethod ?? "S256", "S256", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(request.CodeChallengeMethod, "plain", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateRedirectError(client, request.RedirectUri, request.State, "invalid_request", "仅支持 S256 或 plain 的 code_challenge_method。");
                }
            }

            var authResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!authResult.Succeeded || authResult.Principal?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new
                {
                    error = "interaction_required",
                    message = "用户尚未登录，请先完成登录后再继续授权。",
                    client = new { client.ClientId, client.ClientName },
                    authorization_request = request
                });
            }

            var userIdValue = authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? authResult.Principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized(new { error = "invalid_token", message = "当前登录态缺少有效用户标识。" });
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { error = "invalid_token", message = "当前用户不存在或已停用。" });
            }

            if (string.Equals(request.Consent, "deny", StringComparison.OrdinalIgnoreCase))
            {
                await _auditLogService.WriteAsync(
                    "授权",
                    "拒绝授权",
                    user.UserName,
                    client.ClientName,
                    "成功",
                    $"Scopes: {(scopes.Count == 0 ? "无" : string.Join(' ', scopes))}",
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    DateTime.UtcNow,
                    cancellationToken);
                return CreateRedirectError(client, request.RedirectUri, request.State, "access_denied", "用户拒绝了本次授权。", includeErrorDescription: false);
            }

            if (!string.Equals(request.Consent, "approve", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    message = "请求合法，可进入授权确认流程。请以 consent=approve 再次调用以签发授权码。",
                    client = new { client.ClientId, client.ClientName, client.ClientType },
                    user = new { user.Id, user.UserName, user.Email },
                    scopes,
                    state = request.State,
                    approve_uri = BuildAuthorizeActionUri(request, "approve"),
                    deny_uri = BuildAuthorizeActionUri(request, "deny")
                });
            }

            var codeValue = _oidcService.CreateOpaqueToken();
            var authorizationCode = new AuthorizationCode(
                Guid.NewGuid(),
                codeValue,
                client.Id,
                user.Id,
                request.RedirectUri.Trim(),
                scopes,
                request.Nonce,
                request.CodeChallenge,
                request.CodeChallengeMethod,
                DateTime.UtcNow.Add(AuthorizationCodeLifetime));

            await _authorizationCodeRepository.AddAsync(authorizationCode, cancellationToken);
            await _authorizationCodeRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "授权",
                "签发授权码",
                user.UserName,
                client.ClientName,
                "成功",
                $"Scopes: {(scopes.Count == 0 ? "无" : string.Join(' ', scopes))}",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                authorizationCode.CreatedAtUtc,
                cancellationToken);

            var callbackUrl = QueryHelpers.AddQueryString(request.RedirectUri.Trim(), "code", codeValue);
            if (!string.IsNullOrWhiteSpace(request.State))
            {
                callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "state", request.State.Trim());
            }

            if (string.Equals(request.ResponseMode, "json", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    code = codeValue,
                    state = request.State,
                    redirect_uri = request.RedirectUri.Trim(),
                    redirect_url = callbackUrl,
                    expires_in = (int)AuthorizationCodeLifetime.TotalSeconds
                });
            }

            return Redirect(callbackUrl);
        }

        [AllowAnonymous]
        [HttpPost("token")]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Token([FromForm] TokenRequest request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.GrantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "unsupported_grant_type", message = "当前仅支持 authorization_code。" });
            }

            if (string.IsNullOrWhiteSpace(request.Code)
                || string.IsNullOrWhiteSpace(request.ClientId)
                || string.IsNullOrWhiteSpace(request.RedirectUri))
            {
                return BadRequest(new { error = "invalid_request", message = "code、client_id 与 redirect_uri 不能为空。" });
            }

            var authorizationCode = await _authorizationCodeRepository.GetByCodeAsync(request.Code.Trim(), cancellationToken);
            if (authorizationCode == null)
            {
                return BadRequest(new { error = "invalid_grant", message = "授权码不存在。" });
            }

            if (authorizationCode.IsConsumed)
            {
                return BadRequest(new { error = "invalid_grant", message = "授权码已被使用。" });
            }

            if (authorizationCode.IsExpired(DateTime.UtcNow))
            {
                return BadRequest(new { error = "invalid_grant", message = "授权码已过期。" });
            }

            if (!string.Equals(authorizationCode.RedirectUri, request.RedirectUri.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "invalid_grant", message = "redirect_uri 与授权码不匹配。" });
            }

            var client = authorizationCode.Client ?? await _clientRepository.GetByIdAsync(authorizationCode.OidcClientId, cancellationToken);
            if (client == null || !client.IsActive || !string.Equals(client.ClientId, request.ClientId.Trim(), StringComparison.Ordinal))
            {
                return BadRequest(new { error = "invalid_client", message = "客户端不存在、已停用或与授权码不匹配。" });
            }

            if (client.RequirePkce)
            {
                if (string.IsNullOrWhiteSpace(request.CodeVerifier) || string.IsNullOrWhiteSpace(authorizationCode.CodeChallenge))
                {
                    return BadRequest(new { error = "invalid_grant", message = "缺少 code_verifier 或 code_challenge。" });
                }

                if (!VerifyPkce(request.CodeVerifier.Trim(), authorizationCode.CodeChallenge, authorizationCode.CodeChallengeMethod))
                {
                    return BadRequest(new { error = "invalid_grant", message = "code_verifier 校验失败。" });
                }
            }

            var user = authorizationCode.User ?? await _userRepository.GetByIdAsync(authorizationCode.UserId);
            if (user == null || !user.IsActive)
            {
                return BadRequest(new { error = "invalid_grant", message = "授权码对应的用户不存在或已停用。" });
            }

            var roles = await _userRepository.GetUserRolesWithPermissionsAsync(user.Id);
            var scopes = authorizationCode.ScopeList;
            var issuer = _signingCredentialsService.Issuer;
            var authTime = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var accessToken = _oidcService.CreateAccessToken(user, roles, scopes, issuer, _signingCredentialsService.SigningCredentials, client.ClientId, AccessTokenLifetime);
            var idToken = _oidcService.CreateIdToken(user, roles, scopes, issuer, _signingCredentialsService.SigningCredentials, client.ClientId, authorizationCode.Nonce, authTime, AccessTokenLifetime);
            var refreshTokenValue = _oidcService.CreateOpaqueToken();
            var refreshToken = new RefreshToken(Guid.NewGuid(), refreshTokenValue, client.Id, user.Id, scopes, DateTime.UtcNow.Add(RefreshTokenLifetime));

            await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
            authorizationCode.Consume(DateTime.UtcNow);
            _authorizationCodeRepository.Update(authorizationCode);
            await _authorizationCodeRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "Token",
                "换取访问令牌",
                user.UserName,
                client.ClientName,
                "成功",
                $"Scopes: {(scopes.Count == 0 ? "无" : string.Join(' ', scopes))}",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                DateTime.UtcNow,
                cancellationToken);

            return Ok(new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = (int)AccessTokenLifetime.TotalSeconds,
                refresh_token = refreshTokenValue,
                id_token = idToken,
                scope = scopes.Count == 0 ? null : string.Join(' ', scopes)
            });
        }

        [Authorize]
        [HttpGet("userinfo")]
        public Task<IActionResult> GetUserInfo(CancellationToken cancellationToken)
        {
            return BuildUserInfoResponse(cancellationToken);
        }

        [Authorize]
        [HttpPost("userinfo")]
        public Task<IActionResult> PostUserInfo(CancellationToken cancellationToken)
        {
            return BuildUserInfoResponse(cancellationToken);
        }

        private async Task<IActionResult> BuildUserInfoResponse(CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized(new { error = "invalid_token", message = "Token 中缺少有效的用户标识。" });
            }

            var scopes = _oidcService.GetScopes(User);
            if (!scopes.Contains(OidcScopes.OpenId, StringComparer.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "insufficient_scope",
                    message = "调用 userinfo 端点需要 openid scope。",
                    required_scope = OidcScopes.OpenId
                });
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { error = "invalid_token", message = "当前 token 对应的用户不存在或已停用。" });
            }

            var roles = await _userRepository.GetUserRolesWithPermissionsAsync(userId);
            var payload = _oidcService.BuildUserInfo(user, roles, scopes);
            return Ok(payload);
        }

        private IActionResult CreateRedirectError(OidcClient? client, string? redirectUri, string? state, string error, string message, bool includeErrorDescription = true)
        {
            if (client == null || string.IsNullOrWhiteSpace(redirectUri) || !client.HasRedirectUri(redirectUri.Trim()))
            {
                return BadRequest(new { error, message });
            }

            var callbackUrl = QueryHelpers.AddQueryString(redirectUri.Trim(), "error", error);
            if (!string.IsNullOrWhiteSpace(state))
            {
                callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "state", state.Trim());
            }

            if (includeErrorDescription)
            {
                callbackUrl = QueryHelpers.AddQueryString(callbackUrl, "error_description", message);
            }

            return Redirect(callbackUrl);
        }

        private static bool VerifyPkce(string codeVerifier, string codeChallenge, string? codeChallengeMethod)
        {
            if (string.Equals(codeChallengeMethod, "plain", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(codeVerifier, codeChallenge, StringComparison.Ordinal);
            }

            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            var computedChallenge = Base64UrlEncoder.Encode(hashedBytes);
            return string.Equals(computedChallenge, codeChallenge, StringComparison.Ordinal);
        }

        private string BuildAuthorizeActionUri(AuthorizeRequest request, string consent)
        {
            var parameters = new Dictionary<string, string?>
            {
                ["response_type"] = request.ResponseType,
                ["client_id"] = request.ClientId,
                ["redirect_uri"] = request.RedirectUri,
                ["scope"] = request.Scope,
                ["state"] = request.State,
                ["nonce"] = request.Nonce,
                ["code_challenge"] = request.CodeChallenge,
                ["code_challenge_method"] = request.CodeChallengeMethod,
                ["consent"] = consent
            };

            var uri = QueryHelpers.AddQueryString("/connect/authorize", parameters!
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(item => item.Key, item => item.Value));

            return uri;
        }
    }

    public class AuthorizeRequest
    {
        [FromQuery(Name = "response_type")]
        public string ResponseType { get; set; } = string.Empty;

        [FromQuery(Name = "client_id")]
        public string ClientId { get; set; } = string.Empty;

        [FromQuery(Name = "redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;

        [FromQuery(Name = "scope")]
        public string? Scope { get; set; }

        [FromQuery(Name = "state")]
        public string? State { get; set; }

        [FromQuery(Name = "nonce")]
        public string? Nonce { get; set; }

        [FromQuery(Name = "code_challenge")]
        public string? CodeChallenge { get; set; }

        [FromQuery(Name = "code_challenge_method")]
        public string? CodeChallengeMethod { get; set; }

        [FromQuery(Name = "consent")]
        public string? Consent { get; set; }

        [FromQuery(Name = "response_mode")]
        public string? ResponseMode { get; set; }
    }

    public class TokenRequest
    {
        [FromForm(Name = "grant_type")]
        public string GrantType { get; set; } = string.Empty;

        [FromForm(Name = "client_id")]
        public string ClientId { get; set; } = string.Empty;

        [FromForm(Name = "code")]
        public string Code { get; set; } = string.Empty;

        [FromForm(Name = "redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;

        [FromForm(Name = "code_verifier")]
        public string? CodeVerifier { get; set; }
    }
}
