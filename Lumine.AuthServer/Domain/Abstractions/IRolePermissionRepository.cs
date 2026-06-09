using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IRolePermissionRepository : IRepository<RolePermission>
    {
        /// <summary>
        /// Get all permissions for a role
        /// </summary>
        Task<List<Permission>> GetPermissionsByRoleIdAsync(Guid roleId);

        /// <summary>
        /// Check if a role has a specific permission
        /// </summary>
        Task<bool> RoleHasPermissionAsync(Guid roleId, Guid permissionId);

        /// <summary>
        /// Get all role permission assignments
        /// </summary>
        Task<List<RolePermission>> GetByRoleIdAsync(Guid roleId);

        /// <summary>
        /// Get all role permission assignments for a permission
        /// </summary>
        Task<List<RolePermission>> GetByPermissionIdAsync(Guid permissionId);

        /// <summary>
        /// Remove a specific role permission assignment
        /// </summary>
        Task RemoveRolePermissionAsync(Guid roleId, Guid permissionId);

        /// <summary>
        /// Remove all permissions from a role
        /// </summary>
        Task RemoveAllRolePermissionsAsync(Guid roleId);
    }
}
