using Microsoft.IdentityModel.Tokens;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public interface IOidcSigningCredentialsService
    {
        string Issuer { get; }

        string KeyId { get; }

        SigningCredentials SigningCredentials { get; }

        SecurityKey ValidationKey { get; }

        JsonWebKey GetJsonWebKey();
    }
}