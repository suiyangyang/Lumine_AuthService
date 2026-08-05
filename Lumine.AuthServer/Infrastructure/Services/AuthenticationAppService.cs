using Lumine.AuthServer.Application.Services;
using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public class AuthenticationAppService : IAuthenticationAppService
    {
        private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(8);
        private readonly IUserRepository _userRepository;
        private readonly IOidcClientRepository _oidcClientRepository;
        private readonly IApiKeyRepository _apiKeyRepository;
        private readonly IPasswordService _passwordService;
        private readonly IOidcService _oidcService;
        private readonly IOidcSigningCredentialsService _signingCredentialsService;
        private readonly IAuditLogService _auditLogService;

        public AuthenticationAppService(
            IUserRepository userRepository,
            IOidcClientRepository oidcClientRepository,
            IApiKeyRepository apiKeyRepository,
            IPasswordService passwordService,
            IOidcService oidcService,
            IOidcSigningCredentialsService signingCredentialsService,
            IAuditLogService auditLogService)
        {
            _userRepository = userRepository;
            _oidcClientRepository = oidcClientRepository;
            _apiKeyRepository = apiKeyRepository;
            _passwordService = passwordService;
            _oidcService = oidcService;
            _signingCredentialsService = signingCredentialsService;
            _auditLogService = auditLogService;
        }

        public async Task<AuthenticationResult> LoginAsync(LoginInput input, CancellationToken cancellationToken = default)
        {
            var (client, scopes) = await ValidateClientAndScopesAsync(input.ClientId, input.Scope, cancellationToken);

            var normalizedUserName = string.IsNullOrWhiteSpace(input.UserName)
                ? throw new InvalidOperationException("invalid_request::用户名或邮箱不能为空。")
                : input.UserName.Trim();

            var user = await _userRepository.GetByUserNameOrEmailAsync(normalizedUserName);
            if (user == null || !user.IsActive)
            {
                await _auditLogService.WriteAsync(
                    "认证",
                    "登录",
                    normalizedUserName,
                    client.ClientId,
                    "失败",
                    "用户名或邮箱不存在，或账号已停用。",
                    occurredAtUtc: DateTime.UtcNow,
                    cancellationToken: cancellationToken);
                throw new UnauthorizedAccessException("用户名或密码错误。");
            }

            if (!_passwordService.VerifyPassword(user, input.Password, out var needsRehash))
            {
                await _auditLogService.WriteAsync(
                    "认证",
                    "登录",
                    user.UserName,
                    client.ClientId,
                    "失败",
                    "密码校验失败。",
                    occurredAtUtc: DateTime.UtcNow,
                    cancellationToken: cancellationToken);
                throw new UnauthorizedAccessException("用户名或密码错误。");
            }

            if (needsRehash)
            {
                user.SetPasswordHash(_passwordService.HashPassword(user, input.Password));
            }

            return await IssueTokenAsync(user, client, scopes, input.Nonce, null, cancellationToken);
        }

        public async Task<AuthenticationResult> LoginWithApiKeyAsync(ApiKeyLoginInput input, CancellationToken cancellationToken = default)
        {
            var (client, scopes) = await ValidateClientAndScopesAsync(input.ClientId, input.Scope, cancellationToken);
            var apiKeyValue = string.IsNullOrWhiteSpace(input.ApiKey)
                ? throw new InvalidOperationException("invalid_request::apiKey 不能为空。")
                : input.ApiKey.Trim();
            var apiKey = await _apiKeyRepository.GetByHashAsync(ApiKey.ComputeHash(apiKeyValue), cancellationToken);
            var now = DateTime.UtcNow;
            if (apiKey == null || !apiKey.IsUsable(client.ClientId, now))
            {
                await _auditLogService.WriteAsync(
                    "认证",
                    "API-KEY换取Token",
                    "API-KEY",
                    client.ClientId,
                    "失败",
                    "API-KEY 无效、已删除、已过期或与 client_id 不匹配。",
                    occurredAtUtc: now,
                    cancellationToken: cancellationToken);
                throw new UnauthorizedAccessException("API-KEY 无效、已删除、已过期或与 client_id 不匹配。");
            }

            var user = apiKey.User ?? await _userRepository.GetByIdAsync(apiKey.UserId);
            if (user == null || !user.IsActive)
            {
                throw new UnauthorizedAccessException("API-KEY 对应的账号不存在或已停用。");
            }

            return await IssueTokenAsync(user, client, scopes, input.Nonce, apiKey, cancellationToken);
        }

        private async Task<(OidcClient Client, IReadOnlyCollection<string> Scopes)> ValidateClientAndScopesAsync(
            string? requestedClientId,
            string? scope,
            CancellationToken cancellationToken)
        {
            var clientId = string.IsNullOrWhiteSpace(requestedClientId)
                ? throw new InvalidOperationException("invalid_client::client_id 不能为空。")
                : requestedClientId.Trim();
            var client = await _oidcClientRepository.GetByClientIdAsync(clientId, cancellationToken);
            if (client == null || !client.IsActive)
            {
                throw new InvalidOperationException("invalid_client::客户端不存在或已停用。");
            }

            IReadOnlyCollection<string> scopes;
            try
            {
                scopes = _oidcService.NormalizeScopes(scope);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"invalid_scope::{ex.Message}");
            }

            if (!client.AllowsScopes(scopes))
            {
                throw new InvalidOperationException("invalid_scope::客户端未被授予所请求的 scope。");
            }

            return (client, scopes);
        }

        private async Task<AuthenticationResult> IssueTokenAsync(
            User user,
            OidcClient client,
            IReadOnlyCollection<string> scopes,
            string? nonce,
            ApiKey? apiKey,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            user.MarkLoginSucceeded(now);
            _userRepository.Update(user);
            if (apiKey != null)
            {
                apiKey.MarkUsed(now);
                _apiKeyRepository.Update(apiKey);
            }

            await _userRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "认证",
                apiKey == null ? "登录" : "API-KEY换取Token",
                user.UserName,
                client.ClientId,
                "成功",
                $"Scopes: {(scopes.Count == 0 ? "无" : string.Join(' ', scopes))}",
                occurredAtUtc: now,
                cancellationToken: cancellationToken);

            var roles = await _userRepository.GetUserRolesWithPermissionsAsync(user.Id);
            var permissions = roles.SelectMany(role => role.RolePermissions)
                .Select(item => item.PermissionName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var authTime = DateTimeOffset.UtcNow;
            var issuer = _signingCredentialsService.Issuer;
            var accessToken = _oidcService.CreateAccessToken(user, roles, scopes, issuer, _signingCredentialsService.SigningCredentials, client.ClientId, AccessTokenLifetime);
            var idToken = _oidcService.CreateIdToken(user, roles, scopes, issuer, _signingCredentialsService.SigningCredentials, client.ClientId, nonce, authTime, AccessTokenLifetime);

            return new AuthenticationResult(
                accessToken,
                idToken,
                scopes.Count == 0 ? null : string.Join(' ', scopes),
                (int)AccessTokenLifetime.TotalSeconds,
                user,
                roles,
                permissions);
        }

        public Task LogoutAsync(LogoutInput input, CancellationToken cancellationToken = default)
        {
            var normalizedUserName = string.IsNullOrWhiteSpace(input.UserName)
                ? "未知用户"
                : input.UserName.Trim();

            return _auditLogService.WriteAsync(
                "认证",
                "登出",
                normalizedUserName,
                string.IsNullOrWhiteSpace(input.ClientId) ? "后台登录" : input.ClientId.Trim(),
                "成功",
                "用户主动退出当前会话。",
                input.IpAddress,
                DateTime.UtcNow,
                cancellationToken);
        }

        public async Task<RegistrationResult> RegisterAsync(RegisterInput input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password))
            {
                throw new InvalidOperationException("invalid_request::用户名、邮箱和密码不能为空。");
            }

            var normalizedUserName = input.UserName.Trim();
            var existingUser = await _userRepository.GetByUserNameAsync(normalizedUserName);
            if (existingUser != null)
            {
                throw new InvalidOperationException($"conflict::用户 '{normalizedUserName}' 已存在。");
            }

            var user = new User(Guid.NewGuid(), normalizedUserName, input.Email.Trim(), string.Empty);
            user.UpdateProfile(normalizedUserName, input.Email.Trim(), input.NickName, input.PhoneNumber, true);
            user.SetPasswordHash(_passwordService.HashPassword(user, input.Password));

            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            return new RegistrationResult(user);
        }
    }
}
