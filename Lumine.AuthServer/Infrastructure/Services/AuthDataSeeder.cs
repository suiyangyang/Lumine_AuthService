using Lumine.AuthServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lumine.AuthServer.Infrastructure.Services
{
    public class AuthDataSeeder
    {
        private readonly AuthDbContext _dbContext;
        private readonly IPasswordService _passwordService;
        private readonly AuthSeedOptions _options;

        public AuthDataSeeder(
            AuthDbContext dbContext,
            IPasswordService passwordService,
            IOptions<AuthSeedOptions> options)
        {
            _dbContext = dbContext;
            _passwordService = passwordService;
            _options = options.Value;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await EnsurePortalMenusAsync(cancellationToken);

            if (!_options.Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.AdminPassword))
            {
                throw new InvalidOperationException("SeedData:AdminPassword must be configured before seeding the default admin user.");
            }

            var permissions = await EnsurePermissionsAsync(cancellationToken);
            var adminRole = await EnsureAdminRoleAsync(permissions, cancellationToken);
            await EnsureAdminUserAsync(adminRole, cancellationToken);
            var configuredRoles = await EnsureConfiguredRolesAsync(permissions, cancellationToken);
            await EnsureConfiguredUsersAsync(configuredRoles, cancellationToken);
            await EnsureClientsAsync(cancellationToken);
        }

        private async Task EnsurePortalMenusAsync(CancellationToken cancellationToken)
        {
            var existingMenus = await _dbContext.PortalMenus.ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;

            foreach (var definition in PortalMenuCatalog.All)
            {
                var menu = existingMenus.FirstOrDefault(item =>
                    string.Equals(item.Key, definition.Key, StringComparison.OrdinalIgnoreCase));

                if (menu == null)
                {
                    _dbContext.PortalMenus.Add(new PortalMenu(
                        Guid.NewGuid(),
                        definition.Key,
                        definition.Title,
                        definition.Section,
                        definition.Route,
                        definition.IconKey,
                        definition.Order,
                        true,
                        definition.IsImplemented,
                        definition.RequiredPermissions,
                        definition.Description,
                        now));
                    continue;
                }

                menu.SynchronizeRegistration(definition.Route, definition.IconKey, definition.IsImplemented);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<Permission>> EnsurePermissionsAsync(CancellationToken cancellationToken)
        {
            var configuredPermissions = _options.Permissions
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var permissionNames = configuredPermissions.Select(item => item.Name.Trim()).ToList();
            var existingPermissions = await _dbContext.Permissions
                .Where(permission => permissionNames.Contains(permission.PermissionName))
                .ToListAsync(cancellationToken);

            foreach (var configuredPermission in configuredPermissions)
            {
                var name = configuredPermission.Name.Trim();
                var existingPermission = existingPermissions.FirstOrDefault(item => item.PermissionName == name);
                if (existingPermission == null)
                {
                    existingPermission = new Permission(Guid.NewGuid(), name, configuredPermission.DisplayName);
                    _dbContext.Permissions.Add(existingPermission);
                    existingPermissions.Add(existingPermission);
                }
                else
                {
                    existingPermission.Update(name, configuredPermission.DisplayName);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existingPermissions;
        }

        private async Task<Role> EnsureAdminRoleAsync(List<Permission> permissions, CancellationToken cancellationToken)
        {
            var roleName = _options.AdminRoleName.Trim();
            var role = await _dbContext.Roles
                .Include(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);

            if (role == null)
            {
                role = new Role(Guid.NewGuid(), roleName);
                _dbContext.Roles.Add(role);
            }

            foreach (var permission in permissions)
            {
                role.AddPermission(permission);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return role;
        }

        private async Task<Dictionary<string, Role>> EnsureConfiguredRolesAsync(
            List<Permission> permissions,
            CancellationToken cancellationToken)
        {
            var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase)
            {
                [_options.AdminRoleName.Trim()] = await _dbContext.Roles
                    .FirstAsync(role => role.Name == _options.AdminRoleName.Trim(), cancellationToken)
            };

            foreach (var configuredRole in _options.Roles
                         .Where(item => !string.IsNullOrWhiteSpace(item.Name)))
            {
                var roleName = configuredRole.Name.Trim();
                var role = await _dbContext.Roles
                    .Include(item => item.RolePermissions)
                    .FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);

                if (role == null)
                {
                    role = new Role(Guid.NewGuid(), roleName);
                    _dbContext.Roles.Add(role);
                }

                var rolePermissions = configuredRole.Permissions
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => permissions.FirstOrDefault(permission =>
                        string.Equals(permission.PermissionName, item.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .Where(permission => permission != null)
                    .Cast<Permission>()
                    .ToArray();

                var missingPermissions = configuredRole.Permissions
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Where(item => !permissions.Any(permission =>
                        string.Equals(permission.PermissionName, item.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (missingPermissions.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Seed role '{roleName}' references unknown permissions: {string.Join(", ", missingPermissions)}");
                }

                role.EnsurePermissions(rolePermissions);
                roles[roleName] = role;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return roles;
        }

        private async Task EnsureAdminUserAsync(Role adminRole, CancellationToken cancellationToken)
        {
            var userName = _options.AdminUserName.Trim();
            var email = _options.AdminEmail.Trim();
            var adminUser = await _dbContext.Users
                .Include(item => item.UserRoles)
                .FirstOrDefaultAsync(item => item.UserName == userName, cancellationToken);

            if (adminUser == null)
            {
                adminUser = new User(Guid.NewGuid(), userName, email, string.Empty);
                adminUser.UpdateProfile(userName, email, "系统管理员", null, true);
                adminUser.SetPasswordHash(_passwordService.HashPassword(adminUser, _options.AdminPassword));
                adminUser.AssignRole(adminRole);
                _dbContext.Users.Add(adminUser);
            }
            else
            {
                adminUser.UpdateProfile(userName, email, adminUser.NickName ?? "系统管理员", adminUser.PhoneNumber, true);

                if (_options.ForceUpdateAdminPassword)
                {
                    adminUser.SetPasswordHash(_passwordService.HashPassword(adminUser, _options.AdminPassword));
                }

                adminUser.AssignRole(adminRole);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureConfiguredUsersAsync(
            IReadOnlyDictionary<string, Role> configuredRoles,
            CancellationToken cancellationToken)
        {
            foreach (var configuredUser in _options.Users
                         .Where(item => !string.IsNullOrWhiteSpace(item.UserName)))
            {
                var userName = configuredUser.UserName.Trim();
                var email = configuredUser.Email.Trim();
                if (string.IsNullOrWhiteSpace(email))
                {
                    throw new InvalidOperationException($"Seed user '{userName}' must define an email.");
                }

                var password = ResolveConfiguredPassword(configuredUser, userName);
                var user = await _dbContext.Users
                    .Include(item => item.UserRoles)
                    .FirstOrDefaultAsync(item => item.UserName == userName, cancellationToken);

                if (user == null)
                {
                    user = new User(Guid.NewGuid(), userName, email, string.Empty);
                    user.SetPasswordHash(_passwordService.HashPassword(user, password));
                    _dbContext.Users.Add(user);
                }
                else if (configuredUser.ForceUpdatePassword)
                {
                    user.SetPasswordHash(_passwordService.HashPassword(user, password));
                }

                user.UpdateProfile(userName, email, configuredUser.NickName, configuredUser.PhoneNumber, configuredUser.IsActive);

                var roleNames = configuredUser.Roles
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var roleName in roleNames)
                {
                    if (!configuredRoles.TryGetValue(roleName, out var role))
                    {
                        throw new InvalidOperationException(
                            $"Seed user '{userName}' references unknown role '{roleName}'.");
                    }

                    user.AssignRole(role);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string ResolveConfiguredPassword(SeedUserOption configuredUser, string userName)
        {
            var password = configuredUser.Password;
            if (string.IsNullOrWhiteSpace(password)
                && !string.IsNullOrWhiteSpace(configuredUser.PasswordEnvironmentVariable))
            {
                password = Environment.GetEnvironmentVariable(configuredUser.PasswordEnvironmentVariable);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Seed user '{userName}' must define Password or PasswordEnvironmentVariable.");
            }

            return password;
        }

        private async Task EnsureClientsAsync(CancellationToken cancellationToken)
        {
            var configuredClients = _options.Clients
                .Where(item => !string.IsNullOrWhiteSpace(item.ClientId))
                .GroupBy(item => item.ClientId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (configuredClients.Count == 0)
            {
                return;
            }

            var clientIds = configuredClients.Select(item => item.ClientId.Trim()).ToList();
            var existingClients = await _dbContext.OidcClients
                .Include(item => item.RedirectUris)
                .Where(item => clientIds.Contains(item.ClientId))
                .ToListAsync(cancellationToken);

            foreach (var configuredClient in configuredClients)
            {
                var normalizedScopes = configuredClient.AllowedScopes
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var unsupportedScopes = normalizedScopes
                    .Where(item => !OidcScopes.SupportedScopes.Contains(item, StringComparer.OrdinalIgnoreCase))
                    .ToArray();

                if (unsupportedScopes.Length > 0)
                {
                    throw new InvalidOperationException($"Seed client '{configuredClient.ClientId}' contains unsupported scopes: {string.Join(", ", unsupportedScopes)}");
                }

                var client = existingClients.FirstOrDefault(item => item.ClientId == configuredClient.ClientId.Trim());
                if (client == null)
                {
                    client = new OidcClient(
                        Guid.NewGuid(),
                        configuredClient.ClientId,
                        configuredClient.ClientName,
                        configuredClient.ClientType,
                        normalizedScopes,
                        configuredClient.RequirePkce,
                        configuredClient.IsActive,
                        configuredClient.Description);
                    client.ReplaceRedirectUris(configuredClient.RedirectUris);
                    _dbContext.OidcClients.Add(client);
                    existingClients.Add(client);
                    continue;
                }

                client.Update(
                    configuredClient.ClientId,
                    configuredClient.ClientName,
                    configuredClient.ClientType,
                    normalizedScopes,
                    configuredClient.RequirePkce,
                    configuredClient.IsActive,
                    configuredClient.Description);
                client.ReplaceRedirectUris(configuredClient.RedirectUris);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
