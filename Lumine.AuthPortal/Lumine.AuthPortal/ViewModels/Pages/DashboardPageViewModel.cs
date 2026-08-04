using System.Globalization;
using System.Text.Json;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;

namespace Lumine.AuthPortal.ViewModels.Pages;

public partial class DashboardPageViewModel : ViewModelBase, IDisposable
{
    private static readonly IBrush UserBrush = Brush.Parse("#60A5FA");
    private static readonly IBrush RoleBrush = Brush.Parse("#A78BFA");
    private static readonly IBrush PermissionBrush = Brush.Parse("#34D399");
    private static readonly IBrush ClientBrush = Brush.Parse("#F59E0B");
    private static readonly IBrush LoginTrendBrush = Brush.Parse("#60A5FA");
    private static readonly IBrush TokenTrendBrush = Brush.Parse("#A78BFA");

    private readonly PortalApiClient _apiClient;
    private readonly PortalSession _session;
    private readonly Action<string>? _openOperationalDetail;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private bool _isDisposed;

    [ObservableProperty]
    private string _backendBaseUrl;

    [ObservableProperty]
    private string _statusMessage = "正在准备系统总览。";

    [ObservableProperty]
    private string _discoverySummary = "尚未读取 OIDC 配置。";

    [ObservableProperty]
    private string _lastUpdatedText = "尚未刷新";

    [ObservableProperty]
    private string _serviceStatusText = "正在加载认证服务。";

    [ObservableProperty]
    private DashboardServiceStatusKind _serviceStatusKind = DashboardServiceStatusKind.Loading;

    [ObservableProperty]
    private string _serviceStatusAccessibilityText = "正在加载认证服务。";

    [ObservableProperty]
    private bool _isConnectionDiagnosticsExpanded;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasLoadedData;

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private string _loadErrorMessage = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<DashboardStatCardItemViewModel> _summaryCards = CreateDefaultSummaryCards();

    [ObservableProperty]
    private IReadOnlyList<DashboardTrendBarItemViewModel> _loginTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();

    [ObservableProperty]
    private IReadOnlyList<DashboardTrendBarItemViewModel> _tokenTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();

    [ObservableProperty]
    private IReadOnlyList<RecentLoginUserItemViewModel> _recentLoginUsers = Array.Empty<RecentLoginUserItemViewModel>();

    [ObservableProperty]
    private IReadOnlyList<DashboardOperationalSignalItemViewModel> _operationalSignalItems = Array.Empty<DashboardOperationalSignalItemViewModel>();

    [ObservableProperty]
    private string _loginTrendHint = "最近 7 天暂无登录趋势数据。";

    [ObservableProperty]
    private string _tokenTrendHint = "最近 7 天暂无 Token 签发数据。";

    [ObservableProperty]
    private string _recentLoginUsersHint = "最近 30 天暂无登录用户记录。";

    public DashboardPageViewModel(PortalApiClient apiClient, PortalSession session, Action<string>? openOperationalDetail = null)
    {
        _apiClient = apiClient;
        _session = session;
        _openOperationalDetail = openOperationalDetail;
        _backendBaseUrl = apiClient.BaseUrl;
        _ = RefreshAsync();
    }

    public bool CanRefresh => !IsBusy;

    public string RefreshButtonText => IsBusy ? "刷新中…" : "刷新";

    public bool ShowLoadingState => IsBusy && !HasLoadedData;

    public bool ShowLoadError => HasLoadError;

    public bool ShowLastUpdated => HasLoadedData;

    public bool ShowConnectionDiagnostics => AppEnvironment.IsDevelopmentBuild;

    public bool ShowConnectionDiagnosticsArea => ShowConnectionDiagnostics && IsConnectionDiagnosticsExpanded;

    public string ConnectionDiagnosticsToggleText => IsConnectionDiagnosticsExpanded ? "收起诊断" : "诊断";

    public string ConnectionDiagnosticsToggleToolTip => IsConnectionDiagnosticsExpanded ? "收起连接诊断" : "展开连接诊断";

    public bool IsServiceStatusLoading => ServiceStatusKind == DashboardServiceStatusKind.Loading;

    public bool IsServiceStatusHealthy => ServiceStatusKind == DashboardServiceStatusKind.Healthy;

    public bool IsServiceStatusWarning => ServiceStatusKind == DashboardServiceStatusKind.Warning;

    public bool IsServiceStatusError => ServiceStatusKind == DashboardServiceStatusKind.Error;

    public bool IsServiceStatusRestricted => ServiceStatusKind == DashboardServiceStatusKind.Restricted;

    public bool HasLoginTrendData => LoginTrendItems.Count > 0;

    public bool HasTokenTrendData => TokenTrendItems.Count > 0;

    public bool HasRecentLoginUsers => RecentLoginUsers.Count > 0;

    public bool ShowLoginTrendEmptyState => !HasLoginTrendData;

    public bool ShowTokenTrendEmptyState => !HasTokenTrendData;

    public bool ShowRecentLoginUsersEmptyState => !HasRecentLoginUsers;

    public bool HasOperationalSignals => OperationalSignalItems.Count > 0;

    public bool ShowOperationalAttention => OperationalAttentionCount > 0;

    public int OperationalAttentionCount => OperationalSignalItems.Sum(item => item.Count);

    public string OperationalAttentionText => $"运营提醒 {OperationalAttentionCount} 项";

    public string OperationalSignalsHint => HasOperationalSignals
        ? "以下统计按当前账号权限展示，数值为服务端生成时的快照。"
        : "当前没有需要关注的运营信号。";

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(RefreshButtonText));
        OnPropertyChanged(nameof(ShowLoadingState));
    }

    partial void OnHasLoadedDataChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLoadingState));
        OnPropertyChanged(nameof(ShowLastUpdated));
    }

    partial void OnHasLoadErrorChanged(bool value) => OnPropertyChanged(nameof(ShowLoadError));

    partial void OnIsConnectionDiagnosticsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowConnectionDiagnosticsArea));
        OnPropertyChanged(nameof(ConnectionDiagnosticsToggleText));
        OnPropertyChanged(nameof(ConnectionDiagnosticsToggleToolTip));
    }

    partial void OnServiceStatusKindChanged(DashboardServiceStatusKind value)
    {
        OnPropertyChanged(nameof(IsServiceStatusLoading));
        OnPropertyChanged(nameof(IsServiceStatusHealthy));
        OnPropertyChanged(nameof(IsServiceStatusWarning));
        OnPropertyChanged(nameof(IsServiceStatusError));
        OnPropertyChanged(nameof(IsServiceStatusRestricted));
    }

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

    partial void OnOperationalSignalItemsChanged(IReadOnlyList<DashboardOperationalSignalItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasOperationalSignals));
        OnPropertyChanged(nameof(ShowOperationalAttention));
        OnPropertyChanged(nameof(OperationalAttentionCount));
        OnPropertyChanged(nameof(OperationalAttentionText));
        OnPropertyChanged(nameof(OperationalSignalsHint));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy || _isDisposed)
        {
            return;
        }

        var cancellationToken = _lifetimeCancellation.Token;
        IsBusy = true;
        HasLoadError = false;
        LoadErrorMessage = string.Empty;
        SetServiceStatus(
            DashboardServiceStatusKind.Loading,
            HasLoadedData ? "正在刷新认证服务。" : "正在加载认证服务。",
            HasLoadedData ? "正在刷新认证服务，页面暂时保留上一次成功数据。" : "正在加载认证服务。");
        try
        {
            if (!AppEnvironment.TryNormalize(BackendBaseUrl, out var normalizedBackendUrl, out var addressError))
            {
                SetLoadError(addressError);
                return;
            }

            BackendBaseUrl = normalizedBackendUrl;
            _apiClient.BaseUrl = normalizedBackendUrl;
            var discovery = await _apiClient.GetDiscoveryAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverySummary = discovery.IsSuccess && discovery.Data != null
                ? $"Issuer: {discovery.Data.Issuer}\nAuthorize: {discovery.Data.AuthorizationEndpoint}\nToken: {discovery.Data.TokenEndpoint}"
                : discovery.ErrorMessage ?? "读取 discovery 失败。";

            if (!discovery.IsSuccess || discovery.Data == null)
            {
                SetLoadError(DiscoverySummary, clearData: !HasLoadedData);
                return;
            }

            if (!_session.IsAuthenticated)
            {
                ResetDashboardState("登录后可查看系统总体信息与趋势图。", keepCards: false);
                SetServiceStatus(
                    DashboardServiceStatusKind.Restricted,
                    "请先登录查看系统概览。",
                    "认证服务可访问，但当前会话未登录，仪表盘数据不可见。先登录后才能查看系统概览。");
                return;
            }

            var summaryResult = await _apiClient.GetDashboardSummaryAsync(_session.AccessToken, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!summaryResult.IsSuccess || summaryResult.Data == null)
            {
                var errorMessage = summaryResult.StatusCode == 401
                    ? "登录状态已失效，请重新登录后再加载仪表盘。"
                    : summaryResult.ErrorMessage ?? "加载仪表盘摘要失败。";
                SetLoadError(errorMessage, clearData: !HasLoadedData);
                return;
            }

            ApplyDashboardSummary(summaryResult.Data);
            HasLoadedData = true;
            LastUpdatedText = $"最近更新：{summaryResult.Data.GeneratedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            StatusMessage = BuildStatusMessage(summaryResult.Data);
            var hasRestrictedModules = HasVisibleRestrictedModules(summaryResult.Data);
            SetServiceStatus(
                hasRestrictedModules
                    ? DashboardServiceStatusKind.Restricted
                    : DashboardServiceStatusKind.Healthy,
                hasRestrictedModules
                    ? "认证服务正常 · 部分模块受限"
                    : "认证服务正常",
                hasRestrictedModules
                    ? $"认证服务和仪表盘摘要均可访问，但当前账号仅可查看部分模块。{StatusMessage}"
                    : "认证服务和仪表盘摘要均可访问，系统概览已更新。");
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (JsonException)
        {
            SetLoadError("仪表盘接口返回的数据格式无法识别，请检查前后端版本是否一致。", clearData: !HasLoadedData);
        }
        catch (Exception ex)
        {
            SetLoadError($"加载仪表盘失败：{ex.Message}", clearData: !HasLoadedData);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleConnectionDiagnostics()
    {
        if (!ShowConnectionDiagnostics)
        {
            return;
        }

        IsConnectionDiagnosticsExpanded = !IsConnectionDiagnosticsExpanded;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
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
        OperationalSignalItems = BuildOperationalSignalItems(summary.OperationalSignals);

        LoginTrendHint = HasLoginTrendData ? "最近 7 天登录趋势。" : "最近 7 天暂无登录趋势数据。";
        TokenTrendHint = HasTokenTrendData ? "最近 7 天 Token 签发趋势。" : "最近 7 天暂无 Token 签发数据。";
        RecentLoginUsersHint = HasRecentLoginUsers ? $"最近登录用户 {RecentLoginUsers.Count} 人。" : "最近 30 天暂无登录用户记录。";
    }

    private void ResetDashboardState(string message, bool keepCards)
    {
        HasLoadedData = false;
        HasLoadError = false;
        LoadErrorMessage = string.Empty;
        if (!keepCards)
        {
            SummaryCards = CreateDefaultSummaryCards();
        }

        LoginTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();
        TokenTrendItems = Array.Empty<DashboardTrendBarItemViewModel>();
        RecentLoginUsers = Array.Empty<RecentLoginUserItemViewModel>();
        OperationalSignalItems = Array.Empty<DashboardOperationalSignalItemViewModel>();
        LoginTrendHint = "最近 7 天暂无登录趋势数据。";
        TokenTrendHint = "最近 7 天暂无 Token 签发数据。";
        RecentLoginUsersHint = "最近 30 天暂无登录用户记录。";
        LastUpdatedText = "尚未刷新";
        StatusMessage = message;
    }

    private void SetLoadError(string message, bool clearData = false)
    {
        var hasPreviousData = HasLoadedData;
        HasLoadError = true;
        LoadErrorMessage = message;
        StatusMessage = hasPreviousData ? "刷新失败，页面仍显示上一次成功加载的数据。" : message;
        SetServiceStatus(
            hasPreviousData && !clearData ? DashboardServiceStatusKind.Warning : DashboardServiceStatusKind.Error,
            hasPreviousData && !clearData ? "刷新失败 · 正在显示上次数据" : "仪表盘暂时不可用",
            hasPreviousData && !clearData
                ? $"本次刷新失败，页面仍显示上一次成功加载的数据。{message}"
                : $"仪表盘加载失败。{message}");
        if (clearData)
        {
            ResetDashboardState(message, keepCards: false);
            HasLoadError = true;
            LoadErrorMessage = message;
        }
    }

    private void SetServiceStatus(DashboardServiceStatusKind kind, string text, string accessibilityText)
    {
        ServiceStatusKind = kind;
        ServiceStatusText = text;
        ServiceStatusAccessibilityText = accessibilityText;
    }

    private static bool HasVisibleRestrictedModules(DashboardSummaryDto summary)
    {
        return new[] { summary.Users, summary.Roles, summary.Permissions, summary.Clients }
            .Any(item => !item.IsVisible);
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

    private IReadOnlyList<DashboardOperationalSignalItemViewModel> BuildOperationalSignalItems(DashboardOperationalSignalsDto? signals)
    {
        if (signals == null)
        {
            return Array.Empty<DashboardOperationalSignalItemViewModel>();
        }

        return new[]
            {
                signals.FailedLogins24h,
                signals.ExpiringRefreshTokens7d,
                signals.ClientConfigurationWarnings,
                signals.ReviewableAuditEvents24h
            }
            .Where(item => item?.IsVisible == true && item.Value.GetValueOrDefault() > 0)
            .Select(item => new DashboardOperationalSignalItemViewModel(
                item!.Key,
                item.Label,
                item.Value!.Value,
                item.Description ?? string.Empty,
                _openOperationalDetail))
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
            new DashboardStatCardItemViewModel("用户总数", "加载中", "正在读取当前用户统计。", UserBrush),
            new DashboardStatCardItemViewModel("角色数量", "加载中", "正在读取当前角色统计。", RoleBrush),
            new DashboardStatCardItemViewModel("权限数量", "加载中", "正在读取当前权限统计。", PermissionBrush),
            new DashboardStatCardItemViewModel("OAuth 客户端数量", "加载中", "正在读取当前客户端统计。", ClientBrush)
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

public enum DashboardServiceStatusKind
{
    Loading,
    Healthy,
    Warning,
    Error,
    Restricted
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

public partial class DashboardOperationalSignalItemViewModel
{
    private readonly Action<string>? _openDetails;

    public DashboardOperationalSignalItemViewModel(string key, string label, int count, string description, Action<string>? openDetails)
    {
        Key = key;
        Label = label;
        Count = count;
        CountText = count.ToString(CultureInfo.InvariantCulture);
        Description = description;
        _openDetails = openDetails;
    }

    public string Key { get; }

    public string Label { get; }

    public int Count { get; }

    public string CountText { get; }

    public string Description { get; }

    public bool CanOpenDetails => _openDetails != null;

    [RelayCommand]
    private void OpenDetails()
    {
        _openDetails?.Invoke(Key);
    }
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
