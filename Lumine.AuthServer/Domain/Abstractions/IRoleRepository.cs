using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IRoleRepository : IRepository<Role>
    {
        Task<Role?> GetByIdAsync(Guid id);
        
        Task<Role?> GetByNameAsync(string name);
        
        Task<List<Role>> GetByIdsAsync(IEnumerable<Guid> ids);

        /// <summary>
        /// Get role with all its permissions
        /// </summary>
        Task<Role?> GetByIdWithPermissionsAsync(Guid id);

        /// <summary>
        /// Get multiple roles with their permissions
        /// </summary>
        Task<List<Role>> GetByIdsWithPermissionsAsync(IEnumerable<Guid> ids);

        /// <summary>
        /// Get all permissions for a role
        /// </summary>
        Task<List<Permission>> GetRolePermissionsAsync(Guid roleId);

        /// <summary>
        /// Check if role has a specific permission
        /// </summary>
        Task<bool> RoleHasPermissionAsync(Guid roleId, Guid permissionId);

        /// <summary>
        /// Get all roles for a user
        /// </summary>
        Task<List<Role>> GetUserRolesAsync(Guid userId);
    }
}
