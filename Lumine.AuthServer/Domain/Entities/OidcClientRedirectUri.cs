using Lumine.SeedWork.Base;

namespace Lumine.AuthServer.Domain.Entities
{
    public class OidcClientRedirectUri : Entity
    {
        public Guid OidcClientId { get; private set; }
        public string RedirectUri { get; private set; } = null!;

        public OidcClient? Client { get; private set; }

        protected OidcClientRedirectUri() { }

        public OidcClientRedirectUri(Guid oidcClientId, string redirectUri)
        {
            OidcClientId = oidcClientId;
            RedirectUri = string.IsNullOrWhiteSpace(redirectUri)
                ? throw new ArgumentException("redirectUri is required.", nameof(redirectUri))
                : redirectUri.Trim();
        }
    }
}
