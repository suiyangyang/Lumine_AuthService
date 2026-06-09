using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Domain.Abstractions;
using Lumine.AuthServer.Domain.Entities;
using Lumine.AuthServer.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;
        private readonly IRoleRepository _roleRepository;
        private readonly IPermissionRepository _permissionRepository;

        public RolesController(AuthDbContext dbContext, IRoleRepository roleRepository, IPermissionRepository permissionRepository)
        {
            _dbContext = dbContext;
            _roleRepository = roleRepository;
            _permissionRepository = permissionRepository;
        }

        [HttpGet]
        [Permission("roles.view")]
        public async Task<ActionResult<PagedResultDto<RoleDto>>> GetAll([FromQuery] string? keyword, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var query = QueryRoles(_dbContext.Roles.AsNoTracking());
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(role => role.Name.Contains(normalizedKeyword));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var roles = await query
                .OrderBy(role => role.Name)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResultDto<RoleDto>
            {
                Items = roles.Select(MapRole).ToArray(),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }

        [HttpGet("{id:guid}")]
        [Permission("roles.view")]
        public async Task<ActionResult<RoleDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var role = await QueryRoles(_dbContext.Roles.AsNoTracking())
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            return role == null ? NotFound() : Ok(MapRole(role));
        }

        [HttpPost]
        [Permission("roles.manage")]
        public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("角色名称不能为空。");
            }

            var normalizedRoleName = request.Name.Trim();
            var existingRole = await _roleRepository.GetByNameAsync(normalizedRoleName);
            if (existingRole != null)
            {
                return Conflict($"角色 '{normalizedRoleName}' 已存在。");
            }

            var permissionIds = request.PermissionIds?.Distinct().ToList() ?? new List<Guid>();
            var permissions = permissionIds.Count == 0
                ? new List<Permission>()
                : await _permissionRepository.GetByIdsAsync(permissionIds);

            if (permissions.Count != permissionIds.Count)
            {
                return BadRequest("存在无效的权限 Id。");
            }

            var role = new Role(Guid.NewGuid(), normalizedRoleName);
            foreach (var permission in permissions)
            {
                role.AddPermission(permission);
            }

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var createdRole = await QueryRoles(_dbContext.Roles.AsNoTracking())
                .FirstAsync(item => item.Id == role.Id, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = role.Id }, MapRole(createdRole));
        }

        [HttpPut("{id:guid}")]
        [Permission("roles.manage")]
        public async Task<ActionResult<RoleDto>> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
        {
            var role = await _dbContext.Roles
                .Include(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (role == null)
            {
                return NotFound();
            }

            var normalizedRoleName = request.Name.Trim();
            var existingRole = await _roleRepository.GetByNameAsync(normalizedRoleName);
            if (existingRole != null && existingRole.Id != id)
            {
                return Conflict($"角色 '{normalizedRoleName}' 已存在。");
            }

            role.Rename(normalizedRoleName);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(MapRole(role));
        }

        [HttpDelete("{id:guid}")]
        [Permission("roles.manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var role = await _dbContext.Roles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (role == null)
            {
                return NotFound();
            }

            _dbContext.Roles.Remove(role);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpPut("{id:guid}/permissions")]
        [Permission("roles.manage")]
        public async Task<ActionResult<RoleDto>> ReplacePermissions(Guid id, [FromBody] ReplaceRolePermissionsRequest request, CancellationToken cancellationToken)
        {
            var role = await _dbContext.Roles
                .Include(item => item.RolePermissions)
                .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (role == null)
            {
                return NotFound();
            }

            var permissionIds = request.PermissionIds.Distinct().ToList();
            var permissions = permissionIds.Count == 0
                ? new List<Permission>()
                : await _dbContext.Permissions.Where(permission => permissionIds.Contains(permission.Id)).ToListAsync(cancellationToken);

            if (permissions.Count != permissionIds.Count)
            {
                return BadRequest("存在无效的权限 Id。");
            }

            var currentPermissionIds = role.RolePermissions.Select(item => item.PermissionId).ToList();
            foreach (var permissionId in currentPermissionIds.Except(permissionIds).ToList())
            {
                role.RemovePermission(permissionId);
            }

            foreach (var permission in permissions)
            {
                role.AddPermission(permission);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var updatedRole = await QueryRoles(_dbContext.Roles.AsNoTracking())
                .FirstAsync(item => item.Id == id, cancellationToken);

            return Ok(MapRole(updatedRole));
        }

        [HttpPost("{id:guid}/permissions/{permissionId:guid}")]
        [Permission("roles.manage")]
        public async Task<ActionResult<RoleDto>> AddPermission(Guid id, Guid permissionId, CancellationToken cancellationToken)
        {
            var role = await _dbContext.Roles
                .Include(item => item.RolePermissions)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (role == null)
            {
                return NotFound();
            }

            var permission = await _permissionRepository.GetByIdAsync(permissionId);
            if (permission == null)
            {
                return NotFound($"权限 '{permissionId}' 不存在。");
            }

            role.AddPermission(permission);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var updatedRole = await QueryRoles(_dbContext.Roles.AsNoTracking())
                .FirstAsync(item => item.Id == id, cancellationToken);

            return Ok(MapRole(updatedRole));
        }

        [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
        [Permission("roles.manage")]
        public async Task<ActionResult<RoleDto>> RemovePermission(Guid id, Guid permissionId, CancellationToken cancellationToken)
        {
            var role = await _dbContext.Roles
                .Include(item => item.RolePermissions)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (role == null)
            {
                return NotFound();
            }

            role.RemovePermission(permissionId);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var updatedRole = await QueryRoles(_dbContext.Roles.AsNoTracking())
                .FirstAsync(item => item.Id == id, cancellationToken);

            return Ok(MapRole(updatedRole));
        }

        private static IQueryable<Role> QueryRoles(IQueryable<Role> queryable)
        {
            return queryable
                .Include(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission);
        }

        private static RoleDto MapRole(Role role)
        {
            return new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Permissions = role.RolePermissions
                    .Where(item => item.Permission != null)
                    .OrderBy(item => item.Permission.PermissionName)
                    .Select(item => new PermissionDto
                    {
                        Id = item.PermissionId,
                        PermissionName = item.Permission.PermissionName,
                        DisplayName = item.Permission.DisplayName
                    })
                    .ToList()
            };
        }
    }

    public record CreateRoleRequest(string Name, List<Guid>? PermissionIds);

    public record UpdateRoleRequest(string Name);

    public record ReplaceRolePermissionsRequest(List<Guid> PermissionIds);
}