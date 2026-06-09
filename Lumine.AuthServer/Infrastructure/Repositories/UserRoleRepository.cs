using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure.Repositories
{
    public class UserRoleRepository : EfRepository<UserRole, AuthDbContext>, IUserRoleRepository
    {
        private readonly AuthDbContext _db;

        public UserRoleRepository(AuthDbContext context) : base(context)
        {
            _db = context;
        }

        public async Task<List<Role>> GetRolesByUserIdAsync(Guid userId)
        {
            return await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role)
                .ToListAsync();
        }

        public async Task<bool> UserHasRoleAsync(Guid userId, Guid roleId)
        {
            return await _db.UserRoles
                .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        }

        public async Task<List<UserRole>> GetByUserIdAsync(Guid userId)
        {
            return await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Include(ur => ur.Role)
                .ToListAsync();
        }

        public async Task<List<UserRole>> GetByRoleIdAsync(Guid roleId)
        {
            return await _db.UserRoles
                .Where(ur => ur.RoleId == roleId)
                .Include(ur => ur.User)
                .ToListAsync();
        }

        public async Task RemoveUserRoleAsync(Guid userId, Guid roleId)
        {
            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

            if (userRole != null)
            {
                _db.UserRoles.Remove(userRole);
                await _db.SaveChangesAsync();
            }
        }

        public async Task RemoveAllUserRolesAsync(Guid userId)
        {
            var userRoles = await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .ToListAsync();

            if (userRoles.Any())
            {
                _db.UserRoles.RemoveRange(userRoles);
                await _db.SaveChangesAsync();
            }
        }
    }
}
