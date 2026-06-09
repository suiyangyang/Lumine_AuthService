using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class RoleRepository : EfRepository<Role, AuthDbContext>, IRoleRepository
    {
        private readonly AuthDbContext _db;
        public RoleRepository(AuthDbContext db) : base(db) { _db = db; }

        public async Task<Role?> GetByIdAsync(Guid id)
            => await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);

        public async Task<Role?> GetByNameAsync(string name)
            => await _db.Roles.FirstOrDefaultAsync(r => r.Name == name);

        public async Task<List<Role>> GetByIdsAsync(IEnumerable<Guid> ids)
            => await _db.Roles.Where(r => ids.Contains(r.Id)).ToListAsync();

        /// <summary>
        /// Get role with all its permissions
        /// </summary>
        public async Task<Role?> GetByIdWithPermissionsAsync(Guid id)
        {
            return await _db.Roles
                .Where(r => r.Id == id)
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get multiple roles with their permissions
        /// </summary>
        public async Task<List<Role>> GetByIdsWithPermissionsAsync(IEnumerable<Guid> ids)
        {
            return await _db.Roles
                .Where(r => ids.Contains(r.Id))
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .ToListAsync();
        }

        /// <summary>
        /// Get all permissions for a role
        /// </summary>
        public async Task<List<Permission>> GetRolePermissionsAsync(Guid roleId)
        {
            return await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.Permission)
                .ToListAsync();
        }

        /// <summary>
        /// Check if role has a specific permission
        /// </summary>
        public async Task<bool> RoleHasPermissionAsync(Guid roleId, Guid permissionId)
        {
            return await _db.RolePermissions
                .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        }

        /// <summary>
        /// Get all roles for a user
        /// </summary>
        public async Task<List<Role>> GetUserRolesAsync(Guid userId)
        {
            return await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role)
                .ToListAsync();
        }
    }
}
