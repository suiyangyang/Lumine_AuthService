using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumine.AuthServer.Domain.Entities
{
    public class RefreshToken : Entity, IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string Token { get; private set; } = null!;
        public Guid OidcClientId { get; private set; }
        public Guid UserId { get; private set; }
        public string Scopes { get; private set; } = string.Empty;
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime ExpiresAtUtc { get; private set; }
        public DateTime? RevokedAtUtc { get; private set; }

        public OidcClient? Client { get; private set; }
        public User? User { get; private set; }

        [NotMapped]
        public IReadOnlyCollection<string> ScopeList => ParseScopes(Scopes);

        protected RefreshToken() { }

        public RefreshToken(Guid id, string token, Guid oidcClientId, Guid userId, IEnumerable<string> scopes, DateTime expiresAtUtc)
        {
            Id = id;
            Token = string.IsNullOrWhiteSpace(token) ? throw new ArgumentException("token is required.", nameof(token)) : token.Trim();
            OidcClientId = oidcClientId;
            UserId = userId;
            Scopes = JoinScopes(scopes);
            CreatedAtUtc = DateTime.UtcNow;
            ExpiresAtUtc = expiresAtUtc;
        }

        public void Revoke(DateTime utcNow)
        {
            RevokedAtUtc ??= utcNow;
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
