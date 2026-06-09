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

        public AuthenticationAppService(
            IUserRepository userRepository,
            IPasswordService passwordService,
            IOidcService oidcService,
            IOidcSigningCredentialsService signingCredentialsService)
        {
            _userRepository = userRepository;
            _passwordService = passwordService;
            _oidcService = oidcService;
            _signingCredentialsService = signingCredentialsService;
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
                throw new UnauthorizedAccessException("用户名或密码错误。");
            }

            if (!_passwordService.VerifyPassword(user, input.Password, out var needsRehash))
            {
                throw new UnauthorizedAccessException("用户名或密码错误。");
            }

            if (needsRehash)
            {
                user.SetPasswordHash(_passwordService.HashPassword(user, input.Password));
            }

            user.MarkLoginSucceeded();
            _userRepository.Update(user);
            await _userRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            var roles = await _userRepository.GetUserRolesWithPermissionsAsync(user.Id);
            var permissions = roles.SelectMany(role => role.RolePermissions)
                .Select(item => item.PermissionName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var authTime = DateTimeOffset.UtcNow;
            var issuer = _signingCredentialsService.Issuer;
            var audience = string.IsNullOrWhiteSpace(input.ClientId) ? "lumine.authserver" : input.ClientId.Trim();
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
