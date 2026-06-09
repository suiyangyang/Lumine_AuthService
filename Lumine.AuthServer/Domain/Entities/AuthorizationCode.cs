using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumine.AuthServer.Domain.Entities
{
    public class AuthorizationCode : Entity, IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string Code { get; private set; } = null!;
        public Guid OidcClientId { get; private set; }
        public Guid UserId { get; private set; }
        public string RedirectUri { get; private set; } = null!;
        public string Scopes { get; private set; } = string.Empty;
        public string? Nonce { get; private set; }
        public string? CodeChallenge { get; private set; }
        public string CodeChallengeMethod { get; private set; } = "S256";
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime ExpiresAtUtc { get; private set; }
        public DateTime? ConsumedAtUtc { get; private set; }

        public OidcClient? Client { get; private set; }
        public User? User { get; private set; }

        [NotMapped]
        public IReadOnlyCollection<string> ScopeList => ParseScopes(Scopes);

        protected AuthorizationCode() { }

        public AuthorizationCode(Guid id, string code, Guid oidcClientId, Guid userId, string redirectUri, IEnumerable<string> scopes, string? nonce, string? codeChallenge, string? codeChallengeMethod, DateTime expiresAtUtc)
        {
            Id = id;
            Code = string.IsNullOrWhiteSpace(code) ? throw new ArgumentException("code is required.", nameof(code)) : code.Trim();
            OidcClientId = oidcClientId;
            UserId = userId;
            RedirectUri = string.IsNullOrWhiteSpace(redirectUri) ? throw new ArgumentException("redirectUri is required.", nameof(redirectUri)) : redirectUri.Trim();
            Scopes = JoinScopes(scopes);
            Nonce = string.IsNullOrWhiteSpace(nonce) ? null : nonce.Trim();
            CodeChallenge = string.IsNullOrWhiteSpace(codeChallenge) ? null : codeChallenge.Trim();
            CodeChallengeMethod = string.IsNullOrWhiteSpace(codeChallengeMethod) ? "S256" : codeChallengeMethod.Trim();
            CreatedAtUtc = DateTime.UtcNow;
            ExpiresAtUtc = expiresAtUtc;
        }

        public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

        public bool IsConsumed => ConsumedAtUtc.HasValue;

        public void Consume(DateTime utcNow)
        {
            if (IsConsumed)
            {
                throw new InvalidOperationException("Authorization code has already been consumed.");
            }

            ConsumedAtUtc = utcNow;
        }

        private static string JoinScopes(IEnumerable<string> scopes)
        {
            return string.Join(' ', ParseScopes(string.Join(' ', scopes ?? Array.Empty<string>())));
        }

        private static IReadOnlyCollection<string> ParseScopes(string? scopes)
        {
            if (string.IsNullOrWhiteSpace(scopes))
            {
                return Array.Empty<string>();
            }

            return scopes
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
