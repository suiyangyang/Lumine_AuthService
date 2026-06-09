using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class PermissionRepository : EfRepository<Permission, AuthDbContext>, IPermissionRepository
    {
        private readonly AuthDbContext _db;
        
        public PermissionRepository(AuthDbContext db) : base(db) { _db = db; }

        public async Task<Permission?> GetByIdAsync(Guid id)
            => await _db.Permissions.FirstOrDefaultAsync(p => p.Id == id);

        public async Task<Permission?> GetByNameAsync(string name)
            => await _db.Permissions.FirstOrDefaultAsync(p => p.PermissionName == name);

        /// <summary>
        /// Get all roles that have a specific permission
        /// </summary>
        public async Task<List<Role>> GetRolesByPermissionIdAsync(Guid permissionId)
        {
            return await _db.RolePermissions
                .Where(rp => rp.PermissionId == permissionId)
                .Select(rp => rp.Role)
                .ToListAsync();
        }

        /// <summary>
        /// Get all users that have a specific permission (through roles)
        /// </summary>
        public async Task<List<User>> GetUsersByPermissionIdAsync(Guid permissionId)
        {
            return await _db.RolePermissions
                .Where(rp => rp.PermissionId == permissionId)
                .SelectMany(rp => rp.Role.UserRoles)
                .Select(ur => ur.User)
                .Distinct()
                .ToListAsync();
        }

        /// <summary>
        /// Get all permissions by multiple IDs
        /// </summary>
        public async Task<List<Permission>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            return await _db.Permissions
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
        }
    }
}
