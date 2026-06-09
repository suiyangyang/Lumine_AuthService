using Lumine.AuthServer.Domain.Entities;
using Lumine.SeedWork.Abstractions;

namespace Lumine.AuthServer.Domain.Abstractions
{
    public interface IPermissionRepository : IRepository<Permission>
    {
        Task<Permission?> GetByIdAsync(Guid id);
        
        Task<Permission?> GetByNameAsync(string name);

        /// <summary>
        /// Get all roles that have a specific permission
        /// </summary>
        Task<List<Role>> GetRolesByPermissionIdAsync(Guid permissionId);

        /// <summary>
        /// Get all users that have a specific permission (through roles)
        /// </summary>
        Task<List<User>> GetUsersByPermissionIdAsync(Guid permissionId);

        /// <summary>
        /// Get all permissions by multiple IDs
        /// </summary>
        Task<List<Permission>> GetByIdsAsync(IEnumerable<Guid> ids);
    }
}
