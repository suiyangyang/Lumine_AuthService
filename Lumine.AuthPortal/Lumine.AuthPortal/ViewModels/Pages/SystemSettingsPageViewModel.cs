using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;

namespace Lumine.AuthPortal.ViewModels.Pages;

public partial class SystemSettingsPageViewModel : ViewModelBase
{
    private readonly PortalApiClient _apiClient;
    private readonly PortalSession _session;
    private readonly ThemeService _themeService;

    public SystemSettingsPageViewModel(PortalApiClient apiClient, PortalSession session, ThemeService themeService)
    {
        _apiClient = apiClient;
        _session = session;
        _themeService = themeService;
        ThemeOptions = _themeService.ThemeOptions
            .Select(option => new ThemeOptionCardViewModel(option, option.Mode == _themeService.SelectedTheme))
            .ToArray();

        _themeService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ThemeService.SelectedTheme))
            {
                RefreshThemeSelection();
            }
        };

        if (_session.IsAuthenticated)
        {
            _ = LoadSummaryAsync();
        }
    }

    public string Title => "系统配置";

    public string Description => "统一管理后台的体验参数。当前已支持切换 Fluent Light 与 VS Code Dark 两套主题。";

    public int DefaultManagementPageSize => PortalUiDefaults.ManagementPageSize;

    public IReadOnlyList<ThemeOptionCardViewModel> ThemeOptions { get; }

    public string CurrentThemeLabel => _themeService.CurrentThemeLabel;

    public string CurrentThemeDescription => _themeService.GetThemeOption(_themeService.SelectedTheme).Description;

    public bool HasServerSummary => ServerSummary != null;

    public IReadOnlyList<SystemSettingCardItemViewModel> SettingCards =>
    [
        new("当前主题", CurrentThemeLabel, CurrentThemeDescription),
        new("管理列表默认分页", $"{DefaultManagementPageSize} 条 / 页", "用户、角色、权限、客户端等后台列表统一使用该默认值。"),
        new("搜索交互", "按需展开", "列表页搜索框改为点击图标后展开，避免占用常驻空间。"),
        new("默认客户端", string.IsNullOrWhiteSpace(ServerSummary?.DefaultClientId) ? "--" : ServerSummary.DefaultClientId, "当前后端种子数据中的默认 OIDC 客户端。")
    ];

    public IReadOnlyList<SystemSettingGroupItemViewModel> SettingGroups =>
    [
        new("体验配置", new[]
        {
            "顶部主题快捷切换按钮",
            "系统配置页主题预设",
            "页面级明暗资源同步"
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

    [ObservableProperty]
    private string _statusMessage = "正在读取系统配置摘要。";

    [ObservableProperty]
    private SystemSettingsSummaryDto? _serverSummary;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private void ApplyTheme(ThemeOptionCardViewModel? option)
    {
        if (option == null)
        {
            return;
        }

        _themeService.ApplyTheme(option.Mode);
        RefreshThemeSelection();
    }

    [RelayCommand]
    private async Task LoadSummaryAsync()
    {
        if (!_session.IsAuthenticated)
        {
            StatusMessage = "请先登录后查看系统配置摘要。";
            ServerSummary = null;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _apiClient.GetSystemSettingsSummaryAsync(_session.AccessToken);
            ServerSummary = result.Data;
            StatusMessage = result.IsSuccess && result.Data != null
                ? $"已读取服务端配置摘要，服务器时间 {result.Data.ServerTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}。"
                : result.ErrorMessage ?? "读取系统配置摘要失败。";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasServerSummary));
            OnPropertyChanged(nameof(SettingCards));
        }
    }

    private void RefreshThemeSelection()
    {
        foreach (var option in ThemeOptions)
        {
            option.IsSelected = option.Mode == _themeService.SelectedTheme;
        }

        OnPropertyChanged(nameof(CurrentThemeLabel));
        OnPropertyChanged(nameof(CurrentThemeDescription));
        OnPropertyChanged(nameof(SettingCards));
    }
}

public sealed partial class ThemeOptionCardViewModel : ObservableObject
{
    public ThemeOptionCardViewModel(ThemeOptionItem option, bool isSelected)
    {
        Mode = option.Mode;
        Key = option.Key;
        DisplayName = option.DisplayName;
        Description = option.Description;
        PreviewBackground = option.PreviewBackground;
        PreviewSurface = option.PreviewSurface;
        PreviewAccent = option.PreviewAccent;
        PreviewText = option.PreviewText;
        IsSelected = isSelected;
    }

    public PortalThemeMode Mode { get; }

    public string Key { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public string PreviewBackground { get; }

    public string PreviewSurface { get; }

    public string PreviewAccent { get; }

    public string PreviewText { get; }

    [ObservableProperty]
    private bool _isSelected;

    public bool CanApply => !IsSelected;

    public string ActionText => IsSelected ? "已启用" : "使用";

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(ActionText));
    }
}

public sealed record SystemSettingCardItemViewModel(string Title, string Value, string Description);

public sealed record SystemSettingGroupItemViewModel(string Title, IReadOnlyList<string> Items);
