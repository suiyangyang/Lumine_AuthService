namespace Lumine.AuthServer.Infrastructure.Services
{
    public class OidcOptions
    {
        public const string SectionName = "Oidc";

        public string Issuer { get; set; } = "http://localhost:5115";

        public string KeyId { get; set; } = "lumine-auth-rsa-key";

        public string? PrivateKeyPem { get; set; }

        public bool GenerateDevelopmentKey { get; set; } = true;
    }
}