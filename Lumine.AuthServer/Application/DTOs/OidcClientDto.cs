namespace Lumine.AuthServer.Application.DTOs
{
    public class OidcClientDto
    {
        public Guid Id { get; init; }
        public string ClientId { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string ClientType { get; init; } = string.Empty;
        public IReadOnlyCollection<string> AllowedScopes { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> RedirectUris { get; init; } = Array.Empty<string>();
        public bool RequirePkce { get; init; }
        public bool IsActive { get; init; }
        public string? Description { get; init; }
    }
}
