namespace Lumine.AuthServer.Application.DTOs
{
    public class UserGroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string GroupType { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int PermissionCount { get; set; }
        public string RoleSummary { get; set; } = string.Empty;
        public string PermissionSummary { get; set; } = string.Empty;
        public IReadOnlyList<UserGroupMemberDto> Members { get; set; } = Array.Empty<UserGroupMemberDto>();
    }

    public class UserGroupMemberDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }

    public class RefreshTokenRecordDto
    {
        public Guid Id { get; set; }
        public string TokenPreview { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public IReadOnlyList<string> Scopes { get; set; } = Array.Empty<string>();
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
    }

    public class PortalMenuItemDto
    {
        public int Order { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public bool IsImplemented { get; set; }
        public IReadOnlyList<string> RequiredPermissions { get; set; } = Array.Empty<string>();
        public bool HasAccess { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class AuditLogEntryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
    }

    public class SystemSettingsSummaryDto
    {
        public string Issuer { get; set; } = string.Empty;
        public string AuthorizationEndpoint { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public bool RefreshTokenGrantAdvertised { get; set; }
        public bool SeedEnabled { get; set; }
        public int UserCount { get; set; }
        public int RoleCount { get; set; }
        public int PermissionCount { get; set; }
        public int ClientCount { get; set; }
        public string DefaultClientId { get; set; } = string.Empty;
        public DateTime ServerTimeUtc { get; set; }
    }
}
