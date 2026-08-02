namespace Lumine.AuthServer.Infrastructure.Services
{
    public static class PortalMenuCatalog
    {
        public static readonly IReadOnlyList<string> Sections =
        [
            "系统概览",
            "身份管理",
            "认证授权",
            "系统管理"
        ];

        public static readonly IReadOnlyList<PortalMenuDefinition> All =
        [
            new(1, "dashboard", "仪表盘", "系统概览", "/overview/dashboard", "dashboard", true, [], "系统总体统计、趋势和最近登录概览。"),
            new(2, "users", "用户管理", "身份管理", "/identity/users", "users", true, ["users.view"], "维护后台账号、状态和角色分配。"),
            new(3, "roles", "角色管理", "身份管理", "/identity/roles", "roles", true, ["roles.view"], "维护角色定义与角色权限。"),
            new(4, "permissions", "权限管理", "身份管理", "/identity/permissions", "permissions", true, ["permissions.view"], "维护权限编码、显示名与模块映射。"),
            new(5, "user-groups", "用户组管理", "身份管理", "/identity/user-groups", "user-groups", true, ["users.view"], "按角色聚合成员与权限，作为用户组视图。"),
            new(6, "clients", "OAuth 客户端", "认证授权", "/auth/clients", "clients", true, ["clients.view"], "维护第三方系统接入所需的客户端参数。"),
            new(7, "consent", "授权确认", "认证授权", "/auth/consent", "consent", true, [], "基于真实 authorize 请求预览、批准或拒绝授权。"),
            new(8, "tokens", "Token 管理", "认证授权", "/auth/tokens", "tokens", true, ["clients.view"], "查看 refresh token 签发、到期与撤销状态。"),
            new(9, "oidc", "OIDC Discovery", "认证授权", "/auth/oidc", "oidc", true, [], "串联 authorize、token 与 userinfo 联调。"),
            new(10, "menus", "菜单管理", "系统管理", "/system/menus", "menus", true, ["permissions.view"], "维护后台菜单顺序、分组、可见性与权限绑定。"),
            new(11, "settings", "系统配置", "系统管理", "/system/settings", "settings", true, [], "查看服务端 OIDC 配置摘要与前端主题偏好。"),
            new(12, "audit", "审计日志", "系统管理", "/system/audit", "audit", true, ["clients.view"], "查询认证、授权和后台管理操作记录。")
        ];
    }

    public sealed record PortalMenuDefinition(
        int Order,
        string Key,
        string Title,
        string Section,
        string Route,
        string IconKey,
        bool IsImplemented,
        IReadOnlyList<string> RequiredPermissions,
        string Description);
}
