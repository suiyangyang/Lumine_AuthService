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
        private readonly IPasswordService _passwordService;
        private readonly IOidcService _oidcService;
        private readonly IOidcSigningCredentialsService _signingCredentialsService;
        private readonly IAuditLogService _auditLogService;

        public AuthenticationAppService(
            IUserRepository userRepository,
            IPasswordService passwordService,
            IOidcService oidcService,
            IOidcSigningCredentialsService signingCredentialsService,
            IAuditLogService auditLogService)
        {
            _userRepository = userRepository;
            _passwordService = passwordService;
            _oidcService = oidcService;
            _signingCredentialsService = signingCredentialsService;
            _auditLogService = auditLogService;
        }

        public async Task<AuthenticationResult> LoginAsync(LoginInput input, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<string> scopes;
            try
            {
                scopes = _oidcService.NormalizeScopes(input.Scope);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"invalid_scope::{ex.Message}");
            }

            var normalizedUserName = string.IsNullOrWhiteSpace(input.UserName)
                ? throw new InvalidOperationException("invalid_request::用户名不能为空。")
                : input.UserName.Trim();

            var user = await _userRepository.GetByUserNameAsync(normalizedUserName);
            if (user == null || !user.IsActive)
            {
                await _auditLogService.WriteAsync(
                    "认证",
                    "登录",
                    normalizedUserName,
                    input.ClientId?.Trim() ?? "后台登录",
                    "失败",
                    "用户名不存在或账号已停用。",
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
                    input.ClientId?.Trim() ?? "后台登录",
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

            var audience = string.IsNullOrWhiteSpace(input.ClientId) ? "lumine.authserver" : input.ClientId.Trim();
            user.MarkLoginSucceeded();
            _userRepository.Update(user);
            await _userRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "认证",
                "登录",
                user.UserName,
                audience,
                "成功",
                $"Scopes: {(scopes.Count == 0 ? "无" : string.Join(' ', scopes))}",
                occurredAtUtc: DateTime.UtcNow,
                cancellationToken: cancellationToken);

            var roles = await _userRepository.GetUserRolesWithPermissionsAsync(user.Id);
            var permissions = roles.SelectMany(role => role.RolePermissions)
                .Select(item => item.PermissionName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var authTime = DateTimeOffset.UtcNow;
            var issuer = _signingCredentialsService.Issuer;
            var accessToken = _oidcService.CreateAccessToken(user, roles, scopes, issuer, _signingCredentialsService.SigningCredentials, audience, AccessTokenLifetime);
            var idToken = _oidcService.CreateIdToken(user, roles, scopes, issuer, _signingCredentialsService.SigningCredentials, audience, input.Nonce, authTime, AccessTokenLifetime);

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
