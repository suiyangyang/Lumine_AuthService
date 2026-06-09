using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public class OidcSigningCredentialsService : IOidcSigningCredentialsService, IDisposable
    {
        private readonly RSA _rsa;
        private readonly RsaSecurityKey _securityKey;

        public OidcSigningCredentialsService(OidcOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Issuer))
            {
                throw new InvalidOperationException("Oidc:Issuer must be configured.");
            }

            Issuer = options.Issuer.TrimEnd('/');
            KeyId = string.IsNullOrWhiteSpace(options.KeyId) ? "lumine-auth-rsa-key" : options.KeyId.Trim();

            _rsa = RSA.Create();
            if (!string.IsNullOrWhiteSpace(options.PrivateKeyPem))
            {
                _rsa.ImportFromPem(options.PrivateKeyPem);
            }
            else
            {
                if (!options.GenerateDevelopmentKey)
                {
                    throw new InvalidOperationException("Oidc:PrivateKeyPem is not configured and GenerateDevelopmentKey is disabled.");
                }

                _rsa.KeySize = 2048;
            }

            _securityKey = new RsaSecurityKey(_rsa)
            {
                KeyId = KeyId
            };

            SigningCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.RsaSha256);
        }

        public string Issuer { get; }

        public string KeyId { get; }

        public SigningCredentials SigningCredentials { get; }

        public SecurityKey ValidationKey => _securityKey;

        public JsonWebKey GetJsonWebKey()
        {
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_securityKey);
            jwk.Kid = KeyId;
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            return jwk;
        }

        public void Dispose()
        {
            _rsa.Dispose();
        }
    }
}