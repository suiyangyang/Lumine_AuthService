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
    [ObservableProperty] private string _groupNameInput = string.Empty;
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private UserGroupDto? _pendingDeleteGroup;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isSearchVisible;

    public bool HasGroups => Items.Count > 0;

    public bool HasSelectedGroup => SelectedItem != null;

    public bool HasPendingDelete => PendingDeleteGroup != null;

    public string GroupEditorTitle => SelectedItem == null ? "新建用户组" : "编辑用户组";

    public string DeleteConfirmationText => PendingDeleteGroup == null
        ? string.Empty
        : $"确认删除用户组“{PendingDeleteGroup.GroupName}”吗？仅当没有成员时才允许删除。";

    public bool ShowEmptyState => !HasGroups;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 个用户组";

    public bool HasSearchKeyword => !string.IsNullOrWhiteSpace(SearchKeyword);

    public string SelectedGroupMembersText => SelectedItem == null
        ? "请选择一个用户组查看成员。"
        : SelectedItem.Members.Count == 0
            ? "当前用户组暂无成员。"
            : string.Join(Environment.NewLine, SelectedItem.Members.Select(member =>
                $"{member.UserName} · {(member.IsActive ? "启用" : "禁用")} · {string.Join("、", member.Roles)}"));

    public string SelectedGroupRoleSummary => SelectedItem == null
        ? "选择用户组后查看角色映射。"
        : string.IsNullOrWhiteSpace(SelectedItem.RoleSummary)
            ? "当前用户组未映射角色。"
            : SelectedItem.RoleSummary;

    public string SelectedGroupPermissionSummary => SelectedItem == null
        ? "选择用户组后查看权限摘要。"
        : string.IsNullOrWhiteSpace(SelectedItem.PermissionSummary)
            ? "当前用户组未配置权限。"
            : SelectedItem.PermissionSummary;

    public string SelectedGroupMetaSummary => SelectedItem == null
        ? "未选择用户组"
        : $"{SelectedItem.GroupType} · {SelectedItem.MemberCount} 名成员 · {SelectedItem.PermissionCount} 项权限";

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry SearchIconData => NavigationIconData.Get(IsSearchVisible ? "close" : "search");

    public StreamGeometry ClearSearchIconData => NavigationIconData.Get("close");

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
        OnPropertyChanged(nameof(SelectedGroupRoleSummary));
        OnPropertyChanged(nameof(SelectedGroupPermissionSummary));
        OnPropertyChanged(nameof(SelectedGroupMetaSummary));
        OnPropertyChanged(nameof(HasSelectedGroup));
        OnPropertyChanged(nameof(GroupEditorTitle));
        if (value != null && !IsEditorOpen)
        {
            GroupNameInput = value.GroupName;
        }
    }

    partial void OnPendingDeleteGroupChanged(UserGroupDto? value)
    {
        OnPropertyChanged(nameof(HasPendingDelete));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    partial void OnSearchKeywordChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchKeyword));
    }

    partial void OnPageIndexChanged(int value)
    {
        NotifyPaginationProperties();
    }

    partial void OnPageSizeChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
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
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        OnPropertyChanged(nameof(SearchIconData));

        if (!IsSearchVisible && !string.IsNullOrWhiteSpace(SearchKeyword))
        {
            SearchKeyword = string.Empty;
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            IsSearchVisible = false;
            OnPropertyChanged(nameof(SearchIconData));
            return;
        }

        SearchKeyword = string.Empty;
        _ = SearchAsync();
    }

    [RelayCommand]
    private void OpenCreateGroup()
    {
        SelectedItem = null;
        GroupNameInput = string.Empty;
        IsEditorOpen = true;
        StatusMessage = "请输入用户组名称。用户组将映射为一个角色。";
    }

    [RelayCommand]
    private void OpenEditGroup()
    {
        if (!EnsureAuthenticated() || SelectedItem == null)
        {
            StatusMessage = "请选择一个用户组后再编辑。";
            return;
        }

        GroupNameInput = SelectedItem.GroupName;
        IsEditorOpen = true;
        StatusMessage = $"正在编辑用户组：{SelectedItem.GroupName}";
    }

    [RelayCommand]
    private void CancelGroupEdit()
    {
        IsEditorOpen = false;
        GroupNameInput = SelectedItem?.GroupName ?? string.Empty;
        StatusMessage = "已取消用户组编辑。";
    }

    [RelayCommand]
    private async Task SaveGroupAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        var normalizedName = GroupNameInput.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            StatusMessage = "用户组名称不能为空。";
            return;
        }

        await RunBusyActionAsync(async () =>
        {
            var result = SelectedItem == null
                ? await ApiClient.CreateRoleAsync(Session.AccessToken, new CreateRoleRequestDto(normalizedName, null))
                : await ApiClient.UpdateRoleAsync(Session.AccessToken, Guid.Parse(SelectedItem.Id), new UpdateRoleRequestDto(normalizedName));

            SetRequestStatus(result.IsSuccess, "用户组已保存。", "保存用户组失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                IsEditorOpen = false;
                await LoadAsync();
            }
        });
    }

    [RelayCommand]
    private void DeleteGroup(UserGroupDto? group)
    {
        if (!EnsureAuthenticated() || group == null)
        {
            return;
        }

        PendingDeleteGroup = group;
        SelectedItem = group;
        StatusMessage = $"待删除用户组：{group.GroupName}";
    }

    [RelayCommand]
    private void CancelGroupDelete()
    {
        PendingDeleteGroup = null;
        StatusMessage = "已取消删除用户组。";
    }

    [RelayCommand]
    private async Task ConfirmGroupDeleteAsync()
    {
        if (!EnsureAuthenticated() || PendingDeleteGroup == null)
        {
            return;
        }

        var group = PendingDeleteGroup;
        await RunBusyActionAsync(async () =>
        {
            var result = await ApiClient.DeleteRoleAsync(Session.AccessToken, Guid.Parse(group.Id));
            SetRequestStatus(result.IsSuccess, "用户组已删除。", "删除用户组失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                PendingDeleteGroup = null;
                IsEditorOpen = false;
                await LoadAsync();
            }
        });
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
    [ObservableProperty] private string _statusFilter = "全部";
    [ObservableProperty] private string _expiresBeforeText = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private RefreshTokenRecordDto? _pendingRevokeToken;
    [ObservableProperty] private bool _isSearchVisible;

    public bool HasTokens => Items.Count > 0;

    public bool ShowEmptyState => !HasTokens;

    public bool HasPendingRevoke => PendingRevokeToken != null;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条 Token 记录";

    public bool HasSearchKeyword => !string.IsNullOrWhiteSpace(SearchKeyword);

    public IReadOnlyList<string> StatusOptions { get; } = ["全部", "有效", "已撤销", "已过期"];

    public string DeleteConfirmationText => PendingRevokeToken == null
        ? string.Empty
        : $"确认撤销 Token {PendingRevokeToken.TokenPreview} 吗？";

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry SearchIconData => NavigationIconData.Get(IsSearchVisible ? "close" : "search");

    public StreamGeometry ClearSearchIconData => NavigationIconData.Get("close");

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

    partial void OnSearchKeywordChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchKeyword));
    }

    partial void OnPageIndexChanged(int value)
    {
        NotifyPaginationProperties();
    }

    partial void OnPageSizeChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
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
    private async Task ApplyFiltersAsync()
    {
        await SearchAsync();
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
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        OnPropertyChanged(nameof(SearchIconData));

        if (!IsSearchVisible && !string.IsNullOrWhiteSpace(SearchKeyword))
        {
            SearchKeyword = string.Empty;
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            IsSearchVisible = false;
            OnPropertyChanged(nameof(SearchIconData));
            return;
        }

        SearchKeyword = string.Empty;
        _ = SearchAsync();
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
            DateTime? expiresBeforeUtc = null;
            if (!string.IsNullOrWhiteSpace(ExpiresBeforeText))
            {
                if (!DateTime.TryParse(ExpiresBeforeText.Trim(), out var expiresBefore))
                {
                    StatusMessage = "过期时间必须是有效日期，例如 2026-12-31。";
                    return;
                }

                expiresBeforeUtc = expiresBefore.Date.ToUniversalTime();
            }

            var result = await ApiClient.GetTokensAsync(
                Session.AccessToken,
                normalizedPageIndex,
                PageSize,
                SearchKeyword,
                StatusFilter == "全部" ? null : StatusFilter,
                expiresBeforeUtc);

            Items = result.Data?.Items ?? Array.Empty<RefreshTokenRecordDto>();
            TotalCount = result.Data?.TotalCount ?? 0;
            PageIndex = result.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = result.Data?.PageSize ?? PageSize;
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

    public string SelectedMenuRouteText => SelectedItem?.Route ?? "--";

    public string SelectedMenuSectionText => SelectedItem?.Section ?? "--";

    public string SelectedMenuStatusText => SelectedItem?.ImplementationStatusText ?? "未选择菜单";

    public string SelectedMenuAccessText => SelectedItem?.AccessStatusText ?? "未选择菜单";

    public string MenuOverviewText => $"共 {Items.Count} 个菜单项";

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    partial void OnItemsChanged(IReadOnlyList<PortalMenuItemDto> value)
    {
        OnPropertyChanged(nameof(HasMenus));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(MenuOverviewText));
    }

    partial void OnSelectedItemChanged(PortalMenuItemDto? value)
    {
        OnPropertyChanged(nameof(PermissionSummaryText));
        OnPropertyChanged(nameof(SelectedMenuRouteText));
        OnPropertyChanged(nameof(SelectedMenuSectionText));
        OnPropertyChanged(nameof(SelectedMenuStatusText));
        OnPropertyChanged(nameof(SelectedMenuAccessText));
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
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isSearchVisible;

    public bool HasLogs => Items.Count > 0;

    public bool ShowEmptyState => !HasLogs;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条审计记录";

    public bool HasSearchKeyword => !string.IsNullOrWhiteSpace(SearchKeyword);

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry SearchIconData => NavigationIconData.Get(IsSearchVisible ? "close" : "search");

    public StreamGeometry ClearSearchIconData => NavigationIconData.Get("close");

    public StreamGeometry PreviousPageIconData => NavigationIconData.Get("chevron-left");

    public StreamGeometry NextPageIconData => NavigationIconData.Get("chevron-right");

    partial void OnItemsChanged(IReadOnlyList<AuditLogEntryDto> value)
    {
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnSearchKeywordChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchKeyword));
    }

    partial void OnPageIndexChanged(int value)
    {
        NotifyPaginationProperties();
    }

    partial void OnPageSizeChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
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
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        OnPropertyChanged(nameof(SearchIconData));

        if (!IsSearchVisible && !string.IsNullOrWhiteSpace(SearchKeyword))
        {
            SearchKeyword = string.Empty;
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            IsSearchVisible = false;
            OnPropertyChanged(nameof(SearchIconData));
            return;
        }

        SearchKeyword = string.Empty;
        _ = SearchAsync();
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
            SelectedItem = currentSelectedId == null
                ? Items.FirstOrDefault()
                : Items.FirstOrDefault(item => string.Equals(item.Id, currentSelectedId, StringComparison.OrdinalIgnoreCase)) ?? Items.FirstOrDefault();

            SetRequestStatus(result.IsSuccess, $"已加载第 {PageIndex} 页，共 {TotalCount} 条审计记录。", "加载审计日志失败。", result.ErrorMessage);
        });
    }
}
