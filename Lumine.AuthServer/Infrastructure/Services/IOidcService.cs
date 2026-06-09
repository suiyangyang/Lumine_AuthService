using Lumine.AuthServer.Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public interface IOidcService
    {
        IReadOnlyCollection<string> NormalizeScopes(string? scope);

        string CreateAccessToken(User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes, string issuer, SigningCredentials signingCredentials, string? audience, TimeSpan lifetime);

        string? CreateIdToken(User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes, string issuer, SigningCredentials signingCredentials, string? audience, string? nonce, DateTimeOffset authTime, TimeSpan lifetime);

        string CreateOpaqueToken(int size = 32);

        IDictionary<string, object> BuildUserInfo(User user, IReadOnlyCollection<Role> roles, IReadOnlyCollection<string> scopes);

        IReadOnlyCollection<string> GetScopes(ClaimsPrincipal principal);
    }
}