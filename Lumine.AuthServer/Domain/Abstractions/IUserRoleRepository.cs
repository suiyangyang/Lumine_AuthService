using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IUserRoleRepository : IRepository<UserRole>
    {
        /// <summary>
        /// Get all roles assigned to a user
        /// </summary>
        Task<List<Role>> GetRolesByUserIdAsync(Guid userId);

        /// <summary>
        /// Check if user has a specific role
        /// </summary>
        Task<bool> UserHasRoleAsync(Guid userId, Guid roleId);

        /// <summary>
        /// Get all user role assignments
        /// </summary>
        Task<List<UserRole>> GetByUserIdAsync(Guid userId);

        /// <summary>
        /// Get all user role assignments for a role
        /// </summary>
        Task<List<UserRole>> GetByRoleIdAsync(Guid roleId);

        /// <summary>
        /// Remove a specific user role assignment
        /// </summary>
        Task RemoveUserRoleAsync(Guid userId, Guid roleId);

        /// <summary>
        /// Remove all roles from a user
        /// </summary>
        Task RemoveAllUserRolesAsync(Guid userId);
    }
}
