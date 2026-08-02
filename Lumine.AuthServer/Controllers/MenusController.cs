using System.Security.Claims;
using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.DTOs;
using Lumine.AuthServer.Domain.Entities;
using Lumine.AuthServer.Infrastructure;
using Lumine.AuthServer.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MenusController : ControllerBase
    {
        private readonly AuthDbContext _dbContext;
        private readonly IAuditLogService _auditLogService;

        public MenusController(AuthDbContext dbContext, IAuditLogService auditLogService)
        {
            _dbContext = dbContext;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<PortalMenuItemDto>>> GetAll(CancellationToken cancellationToken)
        {
            var currentPermissions = GetCurrentPermissions();
            var items = await _dbContext.PortalMenus
                .AsNoTracking()
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Title)
                .ToListAsync(cancellationToken);

            return Ok(items.Select(item => MapMenu(item, currentPermissions)).ToArray());
        }

        [HttpPut("{key}")]
        [Permission("permissions.manage")]
        public async Task<ActionResult<PortalMenuItemDto>> Update(
            string key,
            [FromBody] SavePortalMenuRequestDto request,
            CancellationToken cancellationToken)
        {
            var menu = await _dbContext.PortalMenus
                .FirstOrDefaultAsync(item => item.Key == key, cancellationToken);
            if (menu == null)
            {
                return NotFound();
            }

            if (!PortalMenuCatalog.Sections.Contains(request.Section, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("请选择有效的菜单分组。");
            }

            var requiredPermissions = request.RequiredPermissions
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var requiredPermissionNames = requiredPermissions.ToList();

            var availablePermissions = await _dbContext.Permissions
                .AsNoTracking()
                .Where(item => requiredPermissionNames.Contains(item.PermissionName))
                .Select(item => item.PermissionName)
                .ToListAsync(cancellationToken);
            var unknownPermissions = requiredPermissions
                .Except(availablePermissions, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unknownPermissions.Length > 0)
            {
                return BadRequest($"存在无效权限编码：{string.Join("、", unknownPermissions)}");
            }

            try
            {
                menu.UpdateConfiguration(
                    request.Title,
                    request.Section,
                    request.IsEnabled,
                    requiredPermissions,
                    request.Description,
                    DateTime.UtcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);

                await _auditLogService.WriteAsync(
                    "权限",
                    "修改菜单",
                    User.Identity?.Name ?? "当前用户",
                    menu.Key,
                    "成功",
                    $"标题：{menu.Title}；分组：{menu.Section}；状态：{(menu.IsEnabled ? "启用" : "停用")}；权限：{string.Join(", ", menu.RequiredPermissions)}",
                    cancellationToken: cancellationToken);

                return Ok(MapMenu(menu, GetCurrentPermissions()));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("order")]
        [Permission("permissions.manage")]
        public async Task<ActionResult<IReadOnlyList<PortalMenuItemDto>>> Reorder(
            [FromBody] ReorderPortalMenusRequestDto request,
            CancellationToken cancellationToken)
        {
            var menus = await _dbContext.PortalMenus.ToListAsync(cancellationToken);
            var requestedKeys = request.Keys
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToArray();

            if (requestedKeys.Length != menus.Count
                || requestedKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != menus.Count
                || menus.Any(menu => !requestedKeys.Contains(menu.Key, StringComparer.OrdinalIgnoreCase)))
            {
                return BadRequest("排序请求必须且只能包含全部已注册菜单。");
            }

            var now = DateTime.UtcNow;
            for (var index = 0; index < requestedKeys.Length; index++)
            {
                var menu = menus.First(item => string.Equals(item.Key, requestedKeys[index], StringComparison.OrdinalIgnoreCase));
                menu.SetDisplayOrder(index + 1, now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "权限",
                "调整菜单顺序",
                User.Identity?.Name ?? "当前用户",
                "PortalMenus",
                "成功",
                string.Join(" > ", requestedKeys),
                cancellationToken: cancellationToken);

            return Ok(MapMenus(menus, GetCurrentPermissions()));
        }

        [HttpPost("reset")]
        [Permission("permissions.manage")]
        public async Task<ActionResult<IReadOnlyList<PortalMenuItemDto>>> Reset(CancellationToken cancellationToken)
        {
            var menus = await _dbContext.PortalMenus.ToListAsync(cancellationToken);
            var menusByKey = menus.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            foreach (var definition in PortalMenuCatalog.All)
            {
                if (!menusByKey.TryGetValue(definition.Key, out var menu))
                {
                    menu = new PortalMenu(
                        Guid.NewGuid(),
                        definition.Key,
                        definition.Title,
                        definition.Section,
                        definition.Route,
                        definition.IconKey,
                        definition.Order,
                        true,
                        definition.IsImplemented,
                        definition.RequiredPermissions,
                        definition.Description,
                        now);
                    _dbContext.PortalMenus.Add(menu);
                    menus.Add(menu);
                    continue;
                }

                menu.ResetConfiguration(
                    definition.Title,
                    definition.Section,
                    definition.Route,
                    definition.IconKey,
                    definition.Order,
                    true,
                    definition.IsImplemented,
                    definition.RequiredPermissions,
                    definition.Description,
                    now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "权限",
                "重置菜单",
                User.Identity?.Name ?? "当前用户",
                "PortalMenus",
                "成功",
                "已恢复内置菜单标题、分组、顺序、启用状态和权限绑定。",
                cancellationToken: cancellationToken);

            return Ok(MapMenus(menus, GetCurrentPermissions()));
        }

        private IReadOnlySet<string> GetCurrentPermissions()
        {
            return User.Claims
                .Where(claim =>
                    string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase))
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<PortalMenuItemDto> MapMenus(
            IEnumerable<PortalMenu> menus,
            IReadOnlySet<string> currentPermissions)
        {
            return menus
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Title)
                .Select(item => MapMenu(item, currentPermissions))
                .ToArray();
        }

        private static PortalMenuItemDto MapMenu(PortalMenu menu, IReadOnlySet<string> currentPermissions)
        {
            return new PortalMenuItemDto
            {
                Id = menu.Id,
                Order = menu.DisplayOrder,
                Key = menu.Key,
                Title = menu.Title,
                Section = menu.Section,
                Route = menu.Route,
                IconKey = menu.IconKey,
                IsEnabled = menu.IsEnabled,
                IsImplemented = menu.IsImplemented,
                RequiredPermissions = menu.RequiredPermissions,
                HasAccess = HasAccess(menu.RequiredPermissions, currentPermissions),
                Description = menu.Description,
                UpdatedAtUtc = menu.UpdatedAtUtc
            };
        }

        private static bool HasAccess(IReadOnlyList<string> requiredPermissions, IReadOnlySet<string> currentPermissions)
        {
            return requiredPermissions.Count == 0 || requiredPermissions.Any(currentPermissions.Contains);
        }
    }
}
