using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class RolePermissionRepository : EfRepository<RolePermission, AuthDbContext>, IRolePermissionRepository
    {
        private readonly AuthDbContext _db;
        
        public RolePermissionRepository(AuthDbContext db) : base(db) { _db = db; }

        public async Task<List<Permission>> GetPermissionsByRoleIdAsync(Guid roleId)
        {
            return await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.Permission)
                .ToListAsync();
        }

        public async Task<bool> RoleHasPermissionAsync(Guid roleId, Guid permissionId)
        {
            return await _db.RolePermissions
                .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        }

        public async Task<List<RolePermission>> GetByRoleIdAsync(Guid roleId)
        {
            return await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Include(rp => rp.Permission)
                .ToListAsync();
        }

        public async Task<List<RolePermission>> GetByPermissionIdAsync(Guid permissionId)
        {
            return await _db.RolePermissions
                .Where(rp => rp.PermissionId == permissionId)
                .Include(rp => rp.Role)
                .ToListAsync();
        }

        public async Task RemoveRolePermissionAsync(Guid roleId, Guid permissionId)
        {
            var rolePermission = await _db.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
            
            if (rolePermission != null)
            {
                _db.RolePermissions.Remove(rolePermission);
                await _db.SaveChangesAsync();
            }
        }

        public async Task RemoveAllRolePermissionsAsync(Guid roleId)
        {
            var rolePermissions = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();
            
            if (rolePermissions.Any())
            {
                _db.RolePermissions.RemoveRange(rolePermissions);
                await _db.SaveChangesAsync();
            }
        }
    }
}
