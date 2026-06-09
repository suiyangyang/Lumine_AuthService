using Lumine.SeedWork.Abstractions;
using Lumine.SeedWork.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumine.AuthServer.Domain.Entities
{
    public class OidcClient : Entity, IAggregateRoot
    {
        public Guid Id { get; private set; }
        public string ClientId { get; private set; } = null!;
        public string ClientName { get; private set; } = null!;
        public string ClientType { get; private set; } = ClientTypes.Public;
        public string AllowedScopes { get; private set; } = string.Empty;
        public bool RequirePkce { get; private set; } = true;
        public bool IsActive { get; private set; } = true;
        public string? Description { get; private set; }

        public ICollection<OidcClientRedirectUri> RedirectUris { get; private set; } = new List<OidcClientRedirectUri>();

        [NotMapped]
        public IReadOnlyCollection<string> ScopeList => SplitScopes(AllowedScopes);

        protected OidcClient() { }

        public OidcClient(Guid id, string clientId, string clientName, string clientType, IEnumerable<string> scopes, bool requirePkce, bool isActive, string? description = null)
        {
            Id = id;
            Update(clientId, clientName, clientType, scopes, requirePkce, isActive, description);
        }

        public void Update(string clientId, string clientName, string clientType, IEnumerable<string> scopes, bool requirePkce, bool isActive, string? description = null)
        {
            ClientId = NormalizeRequired(clientId, nameof(clientId), 128);
            ClientName = NormalizeRequired(clientName, nameof(clientName), 256);
            ClientType = NormalizeClientType(clientType);
            AllowedScopes = JoinScopes(scopes);
            RequirePkce = requirePkce;
            IsActive = isActive;
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }

        public void ReplaceRedirectUris(IEnumerable<string> redirectUris)
        {
            var normalizedRedirectUris = redirectUris
                .Where(uri => !string.IsNullOrWhiteSpace(uri))
                .Select(uri => uri.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            RedirectUris.Clear();
            foreach (var redirectUri in normalizedRedirectUris)
            {
                RedirectUris.Add(new OidcClientRedirectUri(Id, redirectUri));
            }
        }

        public bool HasRedirectUri(string redirectUri)
        {
            return RedirectUris.Any(item => string.Equals(item.RedirectUri, redirectUri, StringComparison.OrdinalIgnoreCase));
        }

        public bool AllowsScopes(IEnumerable<string> scopes)
        {
            var allowed = ScopeList;
            return scopes.All(scope => allowed.Contains(scope, StringComparer.OrdinalIgnoreCase));
        }

        private static string NormalizeRequired(string value, string paramName, int maxLength)
        {
            var trimmed = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException($"{paramName} is required.", paramName)
                : value.Trim();

            if (trimmed.Length > maxLength)
            {
                throw new ArgumentException($"{paramName} length must be <= {maxLength}.", paramName);
            }

            return trimmed;
        }

        private static string NormalizeClientType(string clientType)
        {
            var normalized = NormalizeRequired(clientType, nameof(clientType), 32).ToLowerInvariant();
            return normalized is ClientTypes.Public or ClientTypes.Confidential
                ? normalized
                : throw new ArgumentException("clientType must be public or confidential.", nameof(clientType));
        }

        private static string JoinScopes(IEnumerable<string> scopes)
        {
            return string.Join(' ', SplitScopes(string.Join(' ', scopes ?? Array.Empty<string>())));
        }

        private static IReadOnlyCollection<string> SplitScopes(string? scopes)
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

    public static class ClientTypes
    {
        public const string Public = "public";
        public const string Confidential = "confidential";
    }
}
