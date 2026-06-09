using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;

namespace Lumine.AuthPortal.ViewModels.Pages;

public partial class UserGroupsManagementPageViewModel : ManagementPageViewModelBase
{
    public UserGroupsManagementPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        if (session.IsAuthenticated)
        {
            _ = LoadAsync();
        }
    }

    [ObservableProperty] private IReadOnlyList<UserGroupDto> _items = Array.Empty<UserGroupDto>();
    [ObservableProperty] private UserGroupDto? _selectedItem;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _selectedPageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50, 100];

    public bool HasGroups => Items.Count > 0;

    public bool ShowEmptyState => !HasGroups;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 个用户组";

    public string SelectedGroupMembersText => SelectedItem == null
        ? "请选择一个用户组查看成员。"
        : SelectedItem.Members.Count == 0
            ? "当前用户组暂无成员。"
            : string.Join(Environment.NewLine, SelectedItem.Members.Select(member =>
                $"{member.UserName} · {(member.IsActive ? "启用" : "禁用")} · {string.Join("、", member.Roles)}"));

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry PreviousPageIconData => NavigationIconData.Get("chevron-left");

    public StreamGeometry NextPageIconData => NavigationIconData.Get("chevron-right");

    partial void OnItemsChanged(IReadOnlyList<UserGroupDto> value)
    {
        OnPropertyChanged(nameof(HasGroups));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnSelectedItemChanged(UserGroupDto? value)
    {
        OnPropertyChanged(nameof(SelectedGroupMembersText));
    }

    partial void OnPageIndexChanged(int value)
    {
        NotifyPaginationProperties();
    }

    partial void OnPageSizeChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
    }

    partial void OnSelectedPageSizeChanged(int value)
    {
        QueuePageSizeChangeIfNeeded(value, PageSize, ApplyPageSizeAsync);
    }

    partial void OnTotalCountChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadCurrentPageAsync(PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await SearchFromFirstPageAsync(LoadPageAsync);
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        await GoToPreviousPageAsync(CanGoPreviousPage, PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        await GoToNextPageAsync(CanGoNextPage, PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task ApplyPageSizeAsync(int? pageSize = null)
    {
        await ApplyPageSizeAndReloadAsync(pageSize, SelectedPageSize, normalizedPageSize =>
        {
            SelectedPageSize = normalizedPageSize;
            PageSize = normalizedPageSize;
        }, LoadPageAsync);
    }

    private async Task LoadPageAsync(int targetPageIndex)
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);
            var currentSelectedId = SelectedItem?.Id;
            var result = await ApiClient.GetUserGroupsAsync(Session.AccessToken, normalizedPageIndex, PageSize, SearchKeyword);

            Items = result.Data?.Items ?? Array.Empty<UserGroupDto>();
            TotalCount = result.Data?.TotalCount ?? 0;
            PageIndex = result.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = result.Data?.PageSize ?? PageSize;
            SelectedPageSize = PageSize;
            SelectedItem = currentSelectedId == null
                ? Items.FirstOrDefault()
                : Items.FirstOrDefault(item => string.Equals(item.Id, currentSelectedId, StringComparison.OrdinalIgnoreCase)) ?? Items.FirstOrDefault();

            SetRequestStatus(result.IsSuccess, $"已加载第 {PageIndex} 页，共 {TotalCount} 个用户组。", "加载用户组失败。", result.ErrorMessage);
        });
    }
}

public partial class TokensManagementPageViewModel : ManagementPageViewModelBase
{
    public TokensManagementPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        if (session.IsAuthenticated)
        {
            _ = LoadAsync();
        }
    }

    [ObservableProperty] private IReadOnlyList<RefreshTokenRecordDto> _items = Array.Empty<RefreshTokenRecordDto>();
    [ObservableProperty] private RefreshTokenRecordDto? _selectedItem;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _selectedPageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private RefreshTokenRecordDto? _pendingRevokeToken;

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50, 100];

    public bool HasTokens => Items.Count > 0;

    public bool ShowEmptyState => !HasTokens;

    public bool HasPendingRevoke => PendingRevokeToken != null;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条 Token 记录";

    public string DeleteConfirmationText => PendingRevokeToken == null
        ? string.Empty
        : $"确认撤销 Token {PendingRevokeToken.TokenPreview} 吗？";

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry PreviousPageIconData => NavigationIconData.Get("chevron-left");

    public StreamGeometry NextPageIconData => NavigationIconData.Get("chevron-right");

    public StreamGeometry ConfirmDeleteIconData => NavigationIconData.Get("trash");

    public StreamGeometry CancelIconData => NavigationIconData.Get("close");

    partial void OnItemsChanged(IReadOnlyList<RefreshTokenRecordDto> value)
    {
        OnPropertyChanged(nameof(HasTokens));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnPendingRevokeTokenChanged(RefreshTokenRecordDto? value)
    {
        OnPropertyChanged(nameof(HasPendingRevoke));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    partial void OnPageIndexChanged(int value)
    {
        NotifyPaginationProperties();
    }

    partial void OnPageSizeChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
    }

    partial void OnSelectedPageSizeChanged(int value)
    {
        QueuePageSizeChangeIfNeeded(value, PageSize, ApplyPageSizeAsync);
    }

    partial void OnTotalCountChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadCurrentPageAsync(PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await SearchFromFirstPageAsync(LoadPageAsync);
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        await GoToPreviousPageAsync(CanGoPreviousPage, PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        await GoToNextPageAsync(CanGoNextPage, PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task ApplyPageSizeAsync(int? pageSize = null)
    {
        await ApplyPageSizeAndReloadAsync(pageSize, SelectedPageSize, normalizedPageSize =>
        {
            SelectedPageSize = normalizedPageSize;
            PageSize = normalizedPageSize;
        }, LoadPageAsync);
    }

    [RelayCommand]
    private void RevokeToken(RefreshTokenRecordDto? token)
    {
        if (!EnsureAuthenticated() || token == null)
        {
            return;
        }

        PendingRevokeToken = token;
        SelectedItem = token;
        StatusMessage = $"待撤销 Token：{token.TokenPreview}";
    }

    [RelayCommand]
    private void CancelDelete()
    {
        CancelPendingDelete(() => PendingRevokeToken = null, "已取消撤销 Token。");
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (!EnsureAuthenticated() || PendingRevokeToken == null)
        {
            return;
        }

        var token = PendingRevokeToken;
        await RunBusyActionAsync(async () =>
        {
            var result = await ApiClient.RevokeTokenAsync(Session.AccessToken, token.Id);
            SetRequestStatus(result.IsSuccess, "Token 已撤销。", "撤销 Token 失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                PendingRevokeToken = null;
                await LoadAsync();
            }
        });
    }

    private async Task LoadPageAsync(int targetPageIndex)
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            PendingRevokeToken = null;
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);
            var currentSelectedId = SelectedItem?.Id;
            var result = await ApiClient.GetTokensAsync(Session.AccessToken, normalizedPageIndex, PageSize, SearchKeyword);

            Items = result.Data?.Items ?? Array.Empty<RefreshTokenRecordDto>();
            TotalCount = result.Data?.TotalCount ?? 0;
            PageIndex = result.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = result.Data?.PageSize ?? PageSize;
            SelectedPageSize = PageSize;
            SelectedItem = FindItemById(Items, currentSelectedId, item => item.Id) ?? Items.FirstOrDefault();

            SetRequestStatus(result.IsSuccess, $"已加载第 {PageIndex} 页，共 {TotalCount} 条 Token 记录。", "加载 Token 记录失败。", result.ErrorMessage);
        });
    }
}

public partial class MenusManagementPageViewModel : ManagementPageViewModelBase
{
    public MenusManagementPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        if (session.IsAuthenticated)
        {
            _ = LoadAsync();
        }
    }

    [ObservableProperty] private IReadOnlyList<PortalMenuItemDto> _items = Array.Empty<PortalMenuItemDto>();
    [ObservableProperty] private PortalMenuItemDto? _selectedItem;

    public bool HasMenus => Items.Count > 0;

    public bool ShowEmptyState => !HasMenus;

    public string PermissionSummaryText => SelectedItem == null
        ? "请选择菜单查看权限信息。"
        : SelectedItem.RequiredPermissions.Count == 0
            ? "当前菜单对所有已登录用户可见。"
            : string.Join(" / ", SelectedItem.RequiredPermissions);

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    partial void OnItemsChanged(IReadOnlyList<PortalMenuItemDto> value)
    {
        OnPropertyChanged(nameof(HasMenus));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnSelectedItemChanged(PortalMenuItemDto? value)
    {
        OnPropertyChanged(nameof(PermissionSummaryText));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var result = await ApiClient.GetMenusAsync(Session.AccessToken);
            Items = result.Data ?? Array.Empty<PortalMenuItemDto>();
            SelectedItem = Items.FirstOrDefault();
            SetRequestStatus(result.IsSuccess, $"已加载 {Items.Count} 个菜单配置。", "加载菜单配置失败。", result.ErrorMessage);
        });
    }
}

public partial class AuditLogsPageViewModel : ManagementPageViewModelBase
{
    public AuditLogsPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        if (session.IsAuthenticated)
        {
            _ = LoadAsync();
        }
    }

    [ObservableProperty] private IReadOnlyList<AuditLogEntryDto> _items = Array.Empty<AuditLogEntryDto>();
    [ObservableProperty] private AuditLogEntryDto? _selectedItem;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _selectedPageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50, 100];

    public bool HasLogs => Items.Count > 0;

    public bool ShowEmptyState => !HasLogs;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条审计记录";

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry PreviousPageIconData => NavigationIconData.Get("chevron-left");

    public StreamGeometry NextPageIconData => NavigationIconData.Get("chevron-right");

    partial void OnItemsChanged(IReadOnlyList<AuditLogEntryDto> value)
    {
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnPageIndexChanged(int value)
    {
        NotifyPaginationProperties();
    }

    partial void OnPageSizeChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
    }

    partial void OnSelectedPageSizeChanged(int value)
    {
        QueuePageSizeChangeIfNeeded(value, PageSize, ApplyPageSizeAsync);
    }

    partial void OnTotalCountChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadCurrentPageAsync(PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await SearchFromFirstPageAsync(LoadPageAsync);
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        await GoToPreviousPageAsync(CanGoPreviousPage, PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        await GoToNextPageAsync(CanGoNextPage, PageIndex, LoadPageAsync);
    }

    [RelayCommand]
    private async Task ApplyPageSizeAsync(int? pageSize = null)
    {
        await ApplyPageSizeAndReloadAsync(pageSize, SelectedPageSize, normalizedPageSize =>
        {
            SelectedPageSize = normalizedPageSize;
            PageSize = normalizedPageSize;
        }, LoadPageAsync);
    }

    private async Task LoadPageAsync(int targetPageIndex)
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);
            var currentSelectedId = SelectedItem?.Id;
            var result = await ApiClient.GetAuditLogsAsync(Session.AccessToken, normalizedPageIndex, PageSize, SearchKeyword);

            Items = result.Data?.Items ?? Array.Empty<AuditLogEntryDto>();
            TotalCount = result.Data?.TotalCount ?? 0;
            PageIndex = result.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = result.Data?.PageSize ?? PageSize;
            SelectedPageSize = PageSize;
            SelectedItem = currentSelectedId == null
                ? Items.FirstOrDefault()
                : Items.FirstOrDefault(item => string.Equals(item.Id, currentSelectedId, StringComparison.OrdinalIgnoreCase)) ?? Items.FirstOrDefault();

            SetRequestStatus(result.IsSuccess, $"已加载第 {PageIndex} 页，共 {TotalCount} 条审计记录。", "加载审计日志失败。", result.ErrorMessage);
        });
    }
}
