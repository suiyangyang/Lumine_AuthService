using Lumine.AuthServer.Domain.Entities;

namespace Lumine.AuthServer.Application.Services
{
    public interface IAuthenticationAppService
    {
        Task<AuthenticationResult> LoginAsync(LoginInput input, CancellationToken cancellationToken = default);

        Task<RegistrationResult> RegisterAsync(RegisterInput input, CancellationToken cancellationToken = default);

        Task LogoutAsync(LogoutInput input, CancellationToken cancellationToken = default);
    }

    public sealed record LoginInput(string UserName, string Password, string? Scope = null, string? ClientId = null, string? Nonce = null);

    public sealed record RegisterInput(string UserName, string Email, string Password, string? NickName = null, string? PhoneNumber = null);

    public sealed record LogoutInput(string UserName, string? ClientId = null, string? IpAddress = null);

    public sealed record AuthenticationResult(
        string AccessToken,
        string? IdToken,
        string? Scope,
        int ExpiresIn,
        User User,
        IReadOnlyCollection<Role> Roles,
        IReadOnlyCollection<string> Permissions);

    public sealed record RegistrationResult(User User);
}
