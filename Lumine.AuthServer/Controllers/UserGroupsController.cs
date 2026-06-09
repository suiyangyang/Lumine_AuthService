using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UserGroupsController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;

        public UserGroupsController(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        [Permission("users.view")]
        public async Task<ActionResult<PagedResultDto<UserGroupDto>>> GetAll(
            [FromQuery] string? keyword,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = ManagementApiDefaults.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? ManagementApiDefaults.DefaultPageSize : Math.Min(pageSize, ManagementApiDefaults.MaxPageSize);

            var roles = await _dbContext.Roles
                .AsNoTracking()
                .Include(role => role.UserRoles)
                    .ThenInclude(userRole => userRole.User)
                .Include(role => role.RolePermissions)
                    .ThenInclude(rolePermission => rolePermission.Permission)
                .ToListAsync(cancellationToken);

            var items = roles
                .Select(MapRoleGroup)
                .OrderBy(item => item.GroupName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                items = items
                    .Where(item =>
                        item.GroupName.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.GroupType.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.RoleSummary.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                        || item.PermissionSummary.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var totalCount = items.Count;
            var pagedItems = items
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return Ok(new PagedResultDto<UserGroupDto>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }

        private static UserGroupDto MapRoleGroup(Domain.Entities.Role role)
        {
            var members = role.UserRoles
                .Where(item => item.User != null)
                .Select(item => item.User!)
                .OrderBy(item => item.UserName, StringComparer.OrdinalIgnoreCase)
                .Select(user => new UserGroupMemberDto
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    IsActive = user.IsActive,
                    LastLoginAtUtc = user.LastLoginAtUtc,
                    Roles = [role.Name]
                })
                .ToArray();

            var permissionNames = role.RolePermissions
                .Where(item => item.Permission != null)
                .Select(item => item.Permission!.DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new UserGroupDto
            {
                Id = role.Id.ToString(),
                GroupName = role.Name,
                GroupType = "角色用户组",
                MemberCount = members.Length,
                PermissionCount = permissionNames.Length,
                RoleSummary = role.Name,
                PermissionSummary = permissionNames.Length == 0
                    ? "未分配权限"
                    : string.Join("、", permissionNames.Take(3)) + (permissionNames.Length > 3 ? $" 等 {permissionNames.Length} 项" : string.Empty),
                Members = members
            };
        }
    }
}
