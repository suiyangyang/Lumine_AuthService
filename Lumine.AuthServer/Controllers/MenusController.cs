using Lumine.AuthServer.Api.Auth;
using Lumine.AuthServer.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lumine.AuthServer.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MenusController : ControllerBase
    {
        [HttpGet]
        [Permission("permissions.view")]
        public ActionResult<IReadOnlyList<PortalMenuItemDto>> GetAll()
        {
            var permissions = User.Claims
                .Where(claim =>
                    string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase))
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var items = new[]
            {
                Create(1, "dashboard", "仪表盘", "系统概览", "/overview/dashboard", true, [], "系统总体统计、趋势和最近登录概览。"),
                Create(2, "users", "用户管理", "身份管理", "/identity/users", true, ["users.view"], "维护后台账号、状态和角色分配。"),
                Create(3, "roles", "角色管理", "身份管理", "/identity/roles", true, ["roles.view"], "维护角色定义与角色权限。"),
                Create(4, "permissions", "权限管理", "身份管理", "/identity/permissions", true, ["permissions.view"], "维护权限编码、显示名与模块映射。"),
                Create(5, "user-groups", "用户组管理", "身份管理", "/identity/user-groups", true, ["users.view"], "按角色聚合成员与权限，作为用户组视图。"),
                Create(6, "clients", "OAuth 客户端", "认证授权", "/auth/clients", true, ["clients.view"], "维护第三方系统接入所需的客户端参数。"),
                Create(7, "consent", "授权确认", "认证授权", "/auth/consent", true, [], "基于真实 authorize 请求预览、批准或拒绝授权。"),
                Create(8, "tokens", "Token 管理", "认证授权", "/auth/tokens", true, ["clients.view"], "查看 refresh token 签发、到期与撤销状态。"),
                Create(9, "oidc", "OIDC Discovery", "认证授权", "/auth/oidc", true, [], "串联 authorize、token 与 userinfo 联调。"),
                Create(10, "menus", "菜单管理", "系统管理", "/system/menus", true, ["permissions.view"], "查看后台菜单顺序、路由、权限绑定与实现状态。"),
                Create(11, "settings", "系统配置", "系统管理", "/system/settings", true, [], "查看服务端 OIDC 配置摘要与前端主题偏好。"),
                Create(12, "audit", "审计日志", "系统管理", "/system/audit", true, ["clients.view"], "基于授权码、refresh token 和管理对象生成审计视图。")
            };

            foreach (var item in items)
            {
                item.HasAccess = HasAccess(item.RequiredPermissions, permissions);
            }

            return Ok(items);
        }

        private static PortalMenuItemDto Create(
            int order,
            string key,
            string title,
            string section,
            string route,
            bool isImplemented,
            IReadOnlyList<string> requiredPermissions,
            string description)
        {
            return new PortalMenuItemDto
            {
                Order = order,
                Key = key,
                Title = title,
                Section = section,
                Route = route,
                IsImplemented = isImplemented,
                RequiredPermissions = requiredPermissions,
                Description = description
            };
        }

        private static bool HasAccess(IReadOnlyList<string> requiredPermissions, IReadOnlySet<string> currentPermissions)
        {
            return requiredPermissions.Count == 0 || requiredPermissions.Any(currentPermissions.Contains);
        }
    }
}
