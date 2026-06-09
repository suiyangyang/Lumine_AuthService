using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;

namespace Lumine.AuthPortal.ViewModels.Pages;

public partial class DashboardPageViewModel : ViewModelBase
{
    private static readonly IBrush UserBrush = Brush.Parse("#60A5FA");
    private static readonly IBrush RoleBrush = Brush.Parse("#A78BFA");
    private static readonly IBrush PermissionBrush = Brush.Parse("#34D399");
    private static readonly IBrush ClientBrush = Brush.Parse("#F59E0B");
    private static readonly IBrush LoginTrendBrush = Brush.Parse("#60A5FA");
    private static readonly IBrush TokenTrendBrush = Brush.Parse("#A78BFA");

    private readonly PortalApiClient _apiClient;
    private readonly PortalSession _session;

    [ObservableProperty]
    private string _backendBaseUrl;

    [ObservableProperty]
    private string _statusMessage = "正在准备系统总览。";

    [ObservableProperty]
    private string _discoverySummary = "尚未读取 OIDC 配置。";

    [ObservableProperty]
    private string _lastUpdatedText = "尚未刷新";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private IReadOnlyList<DashboardStatCardItemViewModel> _summaryCards = CreateDefaultSummaryCards();

    [ObservableProperty]
    private IReadOnlyList<DashboardTrendBarItemViewModel> _loginTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();

    [ObservableProperty]
    private IReadOnlyList<DashboardTrendBarItemViewModel> _tokenTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();

    [ObservableProperty]
    private IReadOnlyList<RecentLoginUserItemViewModel> _recentLoginUsers = Array.Empty<RecentLoginUserItemViewModel>();

    [ObservableProperty]
    private string _loginTrendHint = "最近 7 天暂无登录趋势数据。";

    [ObservableProperty]
    private string _tokenTrendHint = "最近 7 天暂无 Token 签发数据。";

    [ObservableProperty]
    private string _recentLoginUsersHint = "最近 30 天暂无登录用户记录。";

    public DashboardPageViewModel(PortalApiClient apiClient, PortalSession session)
    {
        _apiClient = apiClient;
        _session = session;
        _backendBaseUrl = apiClient.BaseUrl;
        _ = RefreshAsync();
    }

    public bool CanRefresh => !IsBusy;

    public bool HasLoginTrendData => LoginTrendItems.Count > 0;

    public bool HasTokenTrendData => TokenTrendItems.Count > 0;

    public bool HasRecentLoginUsers => RecentLoginUsers.Count > 0;

    public bool ShowLoginTrendEmptyState => !HasLoginTrendData;

    public bool ShowTokenTrendEmptyState => !HasTokenTrendData;

    public bool ShowRecentLoginUsersEmptyState => !HasRecentLoginUsers;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanRefresh));

    partial void OnLoginTrendItemsChanged(IReadOnlyList<DashboardTrendBarItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasLoginTrendData));
        OnPropertyChanged(nameof(ShowLoginTrendEmptyState));
    }

    partial void OnTokenTrendItemsChanged(IReadOnlyList<DashboardTrendBarItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasTokenTrendData));
        OnPropertyChanged(nameof(ShowTokenTrendEmptyState));
    }

    partial void OnRecentLoginUsersChanged(IReadOnlyList<RecentLoginUserItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasRecentLoginUsers));
        OnPropertyChanged(nameof(ShowRecentLoginUsersEmptyState));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        _apiClient.BaseUrl = BackendBaseUrl;
        try
        {
            var discovery = await _apiClient.GetDiscoveryAsync();
            DiscoverySummary = discovery.IsSuccess && discovery.Data != null
                ? $"Issuer: {discovery.Data.Issuer}\nAuthorize: {discovery.Data.AuthorizationEndpoint}\nToken: {discovery.Data.TokenEndpoint}"
                : discovery.ErrorMessage ?? "读取 discovery 失败。";

            if (!_session.IsAuthenticated)
            {
                ResetDashboardState("登录后可查看系统总体信息与趋势图。", keepCards: false);
                return;
            }

            var summaryResult = await _apiClient.GetDashboardSummaryAsync(_session.AccessToken);
            if (!summaryResult.IsSuccess || summaryResult.Data == null)
            {
                ResetDashboardState(summaryResult.ErrorMessage ?? "加载仪表盘摘要失败。", keepCards: false);
                return;
            }

            ApplyDashboardSummary(summaryResult.Data);
            LastUpdatedText = $"最近更新：{summaryResult.Data.GeneratedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            StatusMessage = BuildStatusMessage(summaryResult.Data);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyDashboardSummary(DashboardSummaryDto summary)
    {
        SummaryCards =
        [
            CreateCard(summary.Users, UserBrush),
            CreateCard(summary.Roles, RoleBrush),
            CreateCard(summary.Permissions, PermissionBrush),
            CreateCard(summary.Clients, ClientBrush)
        ];

        LoginTrendItems = BuildTrendItems(summary.LoginTrend, LoginTrendBrush);
        TokenTrendItems = BuildTrendItems(summary.TokenIssueTrend, TokenTrendBrush);
        RecentLoginUsers = summary.RecentLoginUsers
            .Select(item => new RecentLoginUserItemViewModel(
                GetInitials(item.UserName),
                item.UserName,
                string.IsNullOrWhiteSpace(item.Email) ? "未公开邮箱" : item.Email!,
                string.IsNullOrWhiteSpace(item.ClientName) ? item.ClientId ?? "未知客户端" : item.ClientName!,
                item.LoginAtUtc.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)))
            .ToArray();

        LoginTrendHint = HasLoginTrendData ? "最近 7 天登录趋势。" : "最近 7 天暂无登录趋势数据。";
        TokenTrendHint = HasTokenTrendData ? "最近 7 天 Token 签发趋势。" : "最近 7 天暂无 Token 签发数据。";
        RecentLoginUsersHint = HasRecentLoginUsers ? $"最近登录用户 {RecentLoginUsers.Count} 人。" : "最近 30 天暂无登录用户记录。";
    }

    private void ResetDashboardState(string message, bool keepCards)
    {
        if (!keepCards)
        {
            SummaryCards = CreateDefaultSummaryCards();
        }

        LoginTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();
        TokenTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();
        RecentLoginUsers = Array.Empty<RecentLoginUserItemViewModel>();
        LoginTrendHint = "最近 7 天暂无登录趋势数据。";
        TokenTrendHint = "最近 7 天暂无 Token 签发数据。";
        RecentLoginUsersHint = "最近 30 天暂无登录用户记录。";
        LastUpdatedText = "尚未刷新";
        StatusMessage = message;
    }

    private static DashboardStatCardItemViewModel CreateCard(DashboardMetricDto metric, IBrush accentBrush)
    {
        var valueText = metric.IsVisible ? (metric.Value?.ToString(CultureInfo.InvariantCulture) ?? "0") : "无权限";
        return new DashboardStatCardItemViewModel(metric.Title, valueText, metric.Description ?? string.Empty, accentBrush);
    }

    private static IReadOnlyList<DashboardTrendBarItemViewModel> BuildTrendItems(IReadOnlyList<DashboardTrendPointDto>? trendPoints, IBrush brush)
    {
        if (trendPoints == null || trendPoints.Count == 0)
        {
            return Array.Empty<DashboardTrendBarItemViewModel>();
        }

        var maxValue = Math.Max(1, trendPoints.Max(item => item.Value));
        return trendPoints
            .Select(item => new DashboardTrendBarItemViewModel(
                item.Date.ToString("MM-dd", CultureInfo.InvariantCulture),
                item.Value.ToString(CultureInfo.InvariantCulture),
                20 + (item.Value / (double)maxValue * 96),
                brush))
            .ToArray();
    }

    private static string BuildStatusMessage(DashboardSummaryDto summary)
    {
        var visibleCount = new[] { summary.Users, summary.Roles, summary.Permissions, summary.Clients }.Count(item => item.IsVisible);
        return visibleCount == 4
            ? "系统总体信息加载完成。"
            : $"系统总体信息已加载，可见模块 {visibleCount}/4；其余模块受权限控制。";
    }

    private static IReadOnlyList<DashboardStatCardItemViewModel> CreateDefaultSummaryCards()
    {
        return
        [
            new DashboardStatCardItemViewModel("用户总数", "--", "登录后自动加载。", UserBrush),
            new DashboardStatCardItemViewModel("角色数量", "--", "登录后自动加载。", RoleBrush),
            new DashboardStatCardItemViewModel("权限数量", "--", "登录后自动加载。", PermissionBrush),
            new DashboardStatCardItemViewModel("OAuth 客户端数量", "--", "登录后自动加载。", ClientBrush)
        ];
    }

    private static string GetInitials(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "?";
        }

        var trimmed = userName.Trim();
        return trimmed.Length >= 2 ? trimmed[..2].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }
}

public sealed class DashboardStatCardItemViewModel
{
    public DashboardStatCardItemViewModel(string title, string valueText, string description, IBrush accentBrush)
    {
        Title = title;
        ValueText = valueText;
        Description = description;
        AccentBrush = accentBrush;
    }

    public string Title { get; }

    public string ValueText { get; }

    public string Description { get; }

    public IBrush AccentBrush { get; }
}

public sealed class DashboardTrendBarItemViewModel
{
    public DashboardTrendBarItemViewModel(string label, string valueText, double barHeight, IBrush barBrush)
    {
        Label = label;
        ValueText = valueText;
        BarHeight = barHeight;
        BarBrush = barBrush;
    }

    public string Label { get; }

    public string ValueText { get; }

    public double BarHeight { get; }

    public IBrush BarBrush { get; }
}

public sealed class RecentLoginUserItemViewModel
{
    public RecentLoginUserItemViewModel(string avatarText, string userName, string email, string clientName, string loginTimeText)
    {
        AvatarText = avatarText;
        UserName = userName;
        Email = email;
        ClientName = clientName;
        LoginTimeText = loginTimeText;
    }

    public string AvatarText { get; }

    public string UserName { get; }

    public string Email { get; }

    public string ClientName { get; }

    public string LoginTimeText { get; }
}
