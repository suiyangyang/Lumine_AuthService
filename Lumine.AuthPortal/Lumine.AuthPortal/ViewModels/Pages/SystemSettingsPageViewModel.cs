namespace Lumine.AuthPortal.ViewModels.Pages;

public partial class SystemSettingsPageViewModel : ViewModelBase
{
    public string Title => "系统配置";

    public string Description => "当前先提供配置占位界面，后续可接入认证参数、租户设置、环境变量映射和后台体验配置。";

    public int DefaultManagementPageSize => PortalUiDefaults.ManagementPageSize;

    public IReadOnlyList<SystemSettingCardItemViewModel> SettingCards =>
    [
        new("管理列表默认分页", $"{DefaultManagementPageSize} 条 / 页", "用户、角色、权限、客户端等后台列表统一使用该默认值。"),
        new("搜索交互", "按需展开", "列表页搜索框改为点击图标后展开，避免占用常驻空间。"),
        new("按钮样式", "图标优先", "后台操作按钮统一向图标化过渡，保留悬浮提示。")
    ];

    public IReadOnlyList<SystemSettingGroupItemViewModel> SettingGroups =>
    [
        new("基础配置", new[]
        {
            "后台列表默认分页条数",
            "登录页显示模式",
            "审计保留天数"
        }),
        new("认证与授权", new[]
        {
            "OIDC 调试参数默认值",
            "客户端回调白名单",
            "Token 生命周期策略"
        }),
        new("环境与诊断", new[]
        {
            "环境变量映射",
            "容器调试开关",
            "接口连通性检查"
        })
    ];
}

public sealed record SystemSettingCardItemViewModel(string Title, string Value, string Description);

public sealed record SystemSettingGroupItemViewModel(string Title, IReadOnlyList<string> Items);
