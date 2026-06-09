using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByIdAsync(Guid id);
        
        Task<User?> GetByIdWithRolesAsync(Guid id);
        
        Task<User?> GetByUserNameAsync(string userName);

        /// <summary>
        /// Get all permissions for a user across all their roles
        /// </summary>
        Task<List<Permission>> GetUserPermissionsAsync(Guid userId);

        /// <summary>
        /// Check if user has a specific permission
        /// </summary>
        Task<bool> UserHasPermissionAsync(Guid userId, string permissionName);

        /// <summary>
        /// Get roles with their permissions for a user (for authorization checks)
        /// </summary>
        Task<List<Role>> GetUserRolesWithPermissionsAsync(Guid userId);
    }
}
