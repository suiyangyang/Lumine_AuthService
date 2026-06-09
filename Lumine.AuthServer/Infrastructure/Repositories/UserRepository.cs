using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class UserRepository : EfRepository<User, AuthDbContext>, IUserRepository
    {
        private readonly AuthDbContext _db;
        public UserRepository(AuthDbContext db) : base(db) { _db = db; }

        public async Task<User?> GetByIdAsync(Guid id)
            => await _db.Users.FirstOrDefaultAsync(u => u.Id == id);

        public async Task<User?> GetByIdWithRolesAsync(Guid id)
        {
            return await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetByUserNameAsync(string userName)
            => await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);

        /// <summary>
        /// Get all permissions for a user across all their roles
        /// </summary>
        public async Task<List<Permission>> GetUserPermissionsAsync(Guid userId)
        {
            return await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission)
                .Distinct()
                .ToListAsync();
        }

        /// <summary>
        /// Check if user has a specific permission
        /// </summary>
        public async Task<bool> UserHasPermissionAsync(Guid userId, string permissionName)
        {
            return await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .AnyAsync(rp => rp.PermissionName == permissionName);
        }

        /// <summary>
        /// Get roles with their permissions for a user (for authorization checks)
        /// </summary>
        public async Task<List<Role>> GetUserRolesWithPermissionsAsync(Guid userId)
        {
            return await _db.Roles
                .Where(r => r.UserRoles.Any(ur => ur.UserId == userId))
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .ToListAsync();
        }
    }
}
