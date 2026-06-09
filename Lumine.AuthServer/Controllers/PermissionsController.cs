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
    public class PermissionsController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;
        private readonly IPermissionRepository _permissionRepository;

        public PermissionsController(AuthDbContext dbContext, IPermissionRepository permissionRepository)
        {
            _dbContext = dbContext;
            _permissionRepository = permissionRepository;
        }

        [HttpGet]
        [Permission("permissions.view")]
        public async Task<ActionResult<PagedResultDto<PermissionDto>>> GetAll([FromQuery] string? keyword, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex <= 0 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var query = _dbContext.Permissions.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(permission => permission.PermissionName.Contains(normalizedKeyword)
                    || permission.DisplayName.Contains(normalizedKeyword));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var permissions = await query
                .OrderBy(permission => permission.PermissionName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(permission => MapPermission(permission))
                .ToListAsync(cancellationToken);

            return Ok(new PagedResultDto<PermissionDto>
            {
                Items = permissions,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }

        [HttpGet("{id:guid}")]
        [Permission("permissions.view")]
        public async Task<ActionResult<PermissionDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var permission = await _dbContext.Permissions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            return permission == null ? NotFound() : Ok(MapPermission(permission));
        }

        [HttpPost]
        [Permission("permissions.manage")]
        public async Task<ActionResult<PermissionDto>> Create([FromBody] CreatePermissionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PermissionName))
            {
                return BadRequest("权限名称不能为空。");
            }

            var normalizedName = request.PermissionName.Trim();
            var existingPermission = await _permissionRepository.GetByNameAsync(normalizedName);
            if (existingPermission != null)
            {
                return Conflict($"权限 '{normalizedName}' 已存在。");
            }

            var permission = new Permission(Guid.NewGuid(), normalizedName, request.DisplayName);
            _dbContext.Permissions.Add(permission);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = permission.Id }, MapPermission(permission));
        }

        [HttpPut("{id:guid}")]
        [Permission("permissions.manage")]
        public async Task<ActionResult<PermissionDto>> Update(Guid id, [FromBody] UpdatePermissionRequest request, CancellationToken cancellationToken)
        {
            var permission = await _dbContext.Permissions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (permission == null)
            {
                return NotFound();
            }

            var normalizedName = request.PermissionName.Trim();
            var existingPermission = await _permissionRepository.GetByNameAsync(normalizedName);
            if (existingPermission != null && existingPermission.Id != id)
            {
                return Conflict($"权限 '{normalizedName}' 已存在。");
            }

            permission.Update(normalizedName, request.DisplayName);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(MapPermission(permission));
        }

        [HttpDelete("{id:guid}")]
        [Permission("permissions.manage")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var permission = await _dbContext.Permissions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (permission == null)
            {
                return NotFound();
            }

            _dbContext.Permissions.Remove(permission);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private static PermissionDto MapPermission(Permission permission)
        {
            return new PermissionDto
            {
                Id = permission.Id,
                PermissionName = permission.PermissionName,
                DisplayName = permission.DisplayName
            };
        }
    }

    public record CreatePermissionRequest(string PermissionName, string? DisplayName);

    public record UpdatePermissionRequest(string PermissionName, string? DisplayName);
}