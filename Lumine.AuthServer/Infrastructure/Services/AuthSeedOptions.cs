using Lumine.AuthServer.Domain.Entities;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public class AuthSeedOptions
    {
        public const string SectionName = "SeedData";

        public bool Enabled { get; set; } = true;

        public string AdminUserName { get; set; } = "admin";

        public string AdminEmail { get; set; } = "admin@lumine.local";

        public string AdminPassword { get; set; } = string.Empty;

        public string AdminRoleName { get; set; } = "Admin";

        public bool ForceUpdateAdminPassword { get; set; }

        public List<SeedPermissionOption> Permissions { get; set; } =
        [
            new() { Name = "users.view", DisplayName = "查看用户" },
            new() { Name = "users.manage", DisplayName = "管理用户" },
            new() { Name = "roles.view", DisplayName = "查看角色" },
            new() { Name = "roles.manage", DisplayName = "管理角色" },
            new() { Name = "permissions.view", DisplayName = "查看权限" },
            new() { Name = "permissions.manage", DisplayName = "管理权限" },
            new() { Name = "clients.view", DisplayName = "查看客户端" },
            new() { Name = "clients.manage", DisplayName = "管理客户端" },
            new() { Name = "knowledge.instruments.view", DisplayName = "查询知识库仪器" },
            new() { Name = "knowledge.instruments.manage", DisplayName = "维护知识库仪器元数据" }
        ];

        public List<SeedRoleOption> Roles { get; set; } = [];

        public List<SeedUserOption> Users { get; set; } = [];

        public List<SeedClientOption> Clients { get; set; } =
        [
            new()
            {
                ClientId = "lumine-demo-client",
                ClientName = "Lumine Demo Client",
                ClientType = Domain.Entities.ClientTypes.Public,
                AllowedScopes = [OidcScopes.OpenId, OidcScopes.Profile, OidcScopes.Email, OidcScopes.Roles, OidcScopes.Permissions],
                RedirectUris = ["http://localhost:5173/signin-oidc"],
                RequirePkce = true,
                IsActive = true,
                Description = "默认演示客户端"
            },
            new()
            {
                ClientId = "reportmind",
                ClientName = "ReportMind",
                ClientType = Domain.Entities.ClientTypes.Public,
                AllowedScopes = [OidcScopes.OpenId, OidcScopes.Profile, OidcScopes.Email, OidcScopes.Roles, OidcScopes.Permissions],
                RedirectUris = ["http://localhost:10545/api/auth/oidc/callback"],
                RequirePkce = true,
                IsActive = true,
                Description = "ReportMind OIDC 客户端"
            },
            new()
            {
                ClientId = ApiKeyClientIds.LabChromKnowledge,
                ClientName = "LabChrom Knowledge",
                ClientType = Domain.Entities.ClientTypes.Confidential,
                AllowedScopes = [OidcScopes.OpenId, OidcScopes.Profile, OidcScopes.Email, OidcScopes.Roles, OidcScopes.Permissions],
                RedirectUris = [],
                RequirePkce = false,
                IsActive = true,
                Description = "LabChrom Knowledge API-KEY 客户端"
            }
        ];
    }

    public class SeedPermissionOption
    {
        public string Name { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }

    public class SeedClientOption
    {
        public string ClientId { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;

        public string ClientType { get; set; } = Domain.Entities.ClientTypes.Public;

        public List<string> AllowedScopes { get; set; } = [];

        public List<string> RedirectUris { get; set; } = [];

        public bool RequirePkce { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public string? Description { get; set; }
    }

    public class SeedRoleOption
    {
        public string Name { get; set; } = string.Empty;

        public List<string> Permissions { get; set; } = [];
    }

    public class SeedUserOption
    {
        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? NickName { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Password { get; set; }

        public string? PasswordEnvironmentVariable { get; set; }

        public bool ForceUpdatePassword { get; set; }

        public bool IsActive { get; set; } = true;

        public List<string> Roles { get; set; } = [];
    }
}
