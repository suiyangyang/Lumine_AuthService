using System.Globalization;
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
    private readonly Func<Task>? _onMenusChanged;
    private bool _isApplyingPermissionSelection;

    public MenusManagementPageViewModel(
        PortalApiClient apiClient,
        PortalSession session,
        Func<Task>? onMenusChanged = null) : base(apiClient, session)
    {
        _onMenusChanged = onMenusChanged;
        if (session.IsAuthenticated)
        {
            _ = LoadAsync();
        }
    }

    [ObservableProperty] private IReadOnlyList<PortalMenuItemDto> _items = Array.Empty<PortalMenuItemDto>();
    [ObservableProperty] private PortalMenuItemDto? _selectedItem;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string _sectionFilter = "全部分组";
    [ObservableProperty] private string _statusFilter = "全部状态";
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _section = "系统管理";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private IReadOnlyList<MenuPermissionOptionViewModel> _permissionOptions = Array.Empty<MenuPermissionOptionViewModel>();
    [ObservableProperty] private int _selectedPermissionCount;
    [ObservableProperty] private bool _isResetConfirmationVisible;

    public bool HasMenus => Items.Count > 0;

    public bool HasVisibleItems => VisibleItems.Count > 0;

    public bool ShowEmptyState => VisibleItems.Count == 0;

    public bool HasSelectedItem => SelectedItem != null;

    public bool ShowEditorEmptyState => !HasSelectedItem;

    public bool CanManageMenus => Session.HasPermission("permissions.manage");

    public bool CanEditSelected => CanManageMenus && HasSelectedItem;

    public bool CanMoveUp => CanManageMenus && GetSelectedIndex() > 0;

    public bool CanMoveDown => CanManageMenus && GetSelectedIndex() is var index && index >= 0 && index < Items.Count - 1;

    public string EmptyStateMessage => HasMenus ? "没有匹配的菜单" : "暂无菜单数据";

    public IReadOnlyList<string> SectionOptions { get; } = ["系统概览", "身份管理", "认证授权", "系统管理"];

    public IReadOnlyList<string> SectionFilterOptions { get; } = ["全部分组", "系统概览", "身份管理", "认证授权", "系统管理"];

    public IReadOnlyList<string> StatusFilterOptions { get; } = ["全部状态", "已启用", "已停用"];

    public IReadOnlyList<PortalMenuItemDto> VisibleItems => Items
        .Where(item => SectionFilter == "全部分组" || item.Section == SectionFilter)
        .Where(item => StatusFilter switch
        {
            "已启用" => item.IsEnabled,
            "已停用" => !item.IsEnabled,
            _ => true
        })
        .Where(item => MatchesSearch(item, SearchKeyword))
        .ToArray();

    public string SelectedPermissionSummary => SelectedPermissionCount == 0
        ? "未绑定权限，所有已登录用户均可见。"
        : $"已绑定 {SelectedPermissionCount} 项权限，满足任意一项即可显示。";

    public string SelectedMenuRouteText => SelectedItem?.Route ?? "--";

    public string SelectedMenuKeyText => SelectedItem?.Key ?? "--";

    public string SelectedMenuIconText => SelectedItem?.IconKey ?? "--";

    public string SelectedMenuStatusText => SelectedItem?.ImplementationStatusText ?? "未选择菜单";

    public string SelectedMenuUpdatedAtText => SelectedItem?.UpdatedAtDisplay ?? "--";

    public string MenuOverviewText => string.IsNullOrWhiteSpace(SearchKeyword)
        && SectionFilter == "全部分组"
        && StatusFilter == "全部状态"
        ? $"共 {Items.Count} 个菜单项"
        : $"显示 {VisibleItems.Count} / 共 {Items.Count} 个菜单项";

    public string EnabledMenuCountText => Items.Count(item => item.IsEnabled).ToString(CultureInfo.InvariantCulture);

    public string ProtectedMenuCountText => Items.Count(item => item.RequiredPermissions.Count > 0).ToString(CultureInfo.InvariantCulture);

    public string DisabledMenuCountText => Items.Count(item => !item.IsEnabled).ToString(CultureInfo.InvariantCulture);

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry SaveIconData => NavigationIconData.Get("save");

    public StreamGeometry MoveUpIconData => NavigationIconData.Get("chevron-up");

    public StreamGeometry MoveDownIconData => NavigationIconData.Get("chevron-down");

    public StreamGeometry ResetIconData => NavigationIconData.Get("reset");

    public StreamGeometry CancelIconData => NavigationIconData.Get("close");

    partial void OnItemsChanged(IReadOnlyList<PortalMenuItemDto> value)
    {
        OnPropertyChanged(nameof(HasMenus));
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(EmptyStateMessage));
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(MenuOverviewText));
        OnPropertyChanged(nameof(EnabledMenuCountText));
        OnPropertyChanged(nameof(ProtectedMenuCountText));
        OnPropertyChanged(nameof(DisabledMenuCountText));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    partial void OnSearchKeywordChanged(string value)
    {
        RefreshFilteredItems();
    }

    partial void OnSectionFilterChanged(string value) => RefreshFilteredItems();

    partial void OnStatusFilterChanged(string value) => RefreshFilteredItems();

    partial void OnSelectedItemChanged(PortalMenuItemDto? value)
    {
        if (value == null)
        {
            Title = string.Empty;
            Section = "系统管理";
            Description = string.Empty;
            IsEnabled = true;
            ApplyPermissionSelection([]);
        }
        else
        {
            Title = value.Title;
            Section = value.Section;
            Description = value.Description;
            IsEnabled = value.IsEnabled;
            ApplyPermissionSelection(value.RequiredPermissions);
        }

        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(ShowEditorEmptyState));
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(SelectedMenuRouteText));
        OnPropertyChanged(nameof(SelectedMenuKeyText));
        OnPropertyChanged(nameof(SelectedMenuIconText));
        OnPropertyChanged(nameof(SelectedMenuStatusText));
        OnPropertyChanged(nameof(SelectedMenuUpdatedAtText));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var selectedKey = SelectedItem?.Key;
            var menusTask = ApiClient.GetMenusAsync(Session.AccessToken);
            var permissionsTask = ApiClient.GetPermissionsAsync(Session.AccessToken, 1, 200);
            await Task.WhenAll(menusTask, permissionsTask);

            var menusResult = await menusTask;
            var permissionsResult = await permissionsTask;
            Items = menusResult.Data?.OrderBy(item => item.Order).ToArray() ?? Array.Empty<PortalMenuItemDto>();
            PermissionOptions = (permissionsResult.Data?.Items ?? Array.Empty<PermissionDto>())
                .OrderBy(item => item.PermissionName, StringComparer.OrdinalIgnoreCase)
                .Select(item => new MenuPermissionOptionViewModel(item.PermissionName, item.DisplayName, OnPermissionSelectionChanged))
                .ToArray();

            SelectedItem = null;
            SelectedItem = Items.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
                ?? VisibleItems.FirstOrDefault();

            StatusMessage = menusResult.IsSuccess && permissionsResult.IsSuccess
                ? $"已加载 {Items.Count} 个菜单配置和 {PermissionOptions.Count} 项可绑定权限。"
                : menusResult.ErrorMessage ?? permissionsResult.ErrorMessage ?? "加载菜单管理数据失败。";
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManageMenus)
        {
            StatusMessage = "当前账号没有菜单管理权限。";
            return;
        }

        if (SelectedItem == null)
        {
            StatusMessage = "请先选择需要编辑的菜单。";
            return;
        }

        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Section))
        {
            StatusMessage = "菜单名称和所属分组不能为空。";
            return;
        }

        var selectedKey = SelectedItem.Key;
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var result = await ApiClient.UpdateMenuAsync(
                Session.AccessToken,
                selectedKey,
                new SavePortalMenuRequestDto(
                    Title.Trim(),
                    Section,
                    IsEnabled,
                    GetSelectedPermissions(),
                    Description.Trim()));

            SetRequestStatus(result.IsSuccess, "菜单配置已保存并应用到左侧导航。", "保存菜单配置失败。", result.ErrorMessage);
            if (!result.IsSuccess)
            {
                return;
            }

            await ReloadAndNotifyAsync(selectedKey);
        });
    }

    [RelayCommand]
    private Task MoveUpAsync() => MoveSelectedAsync(-1);

    [RelayCommand]
    private Task MoveDownAsync() => MoveSelectedAsync(1);

    [RelayCommand]
    private void BeginReset()
    {
        if (!CanManageMenus)
        {
            StatusMessage = "当前账号没有菜单管理权限。";
            return;
        }

        IsResetConfirmationVisible = true;
        StatusMessage = "重置会恢复全部内置菜单的标题、分组、顺序、启用状态和权限绑定。";
    }

    [RelayCommand]
    private void CancelReset()
    {
        IsResetConfirmationVisible = false;
        StatusMessage = "已取消重置菜单。";
    }

    [RelayCommand]
    private async Task ConfirmResetAsync()
    {
        if (!CanManageMenus)
        {
            return;
        }

        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var result = await ApiClient.ResetMenusAsync(Session.AccessToken);
            SetRequestStatus(result.IsSuccess, "菜单已恢复为内置默认配置。", "重置菜单失败。", result.ErrorMessage);
            if (!result.IsSuccess)
            {
                return;
            }

            IsResetConfirmationVisible = false;
            Items = result.Data?.OrderBy(item => item.Order).ToArray() ?? Array.Empty<PortalMenuItemDto>();
            SelectedItem = Items.FirstOrDefault();
            if (_onMenusChanged != null)
            {
                await _onMenusChanged();
            }
        });
    }

    private async Task MoveSelectedAsync(int offset)
    {
        if (!CanManageMenus || SelectedItem == null || IsBusy)
        {
            return;
        }

        var currentIndex = GetSelectedIndex();
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= Items.Count)
        {
            return;
        }

        var selectedKey = SelectedItem.Key;
        var reordered = Items.ToList();
        (reordered[currentIndex], reordered[targetIndex]) = (reordered[targetIndex], reordered[currentIndex]);

        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var result = await ApiClient.ReorderMenusAsync(
                Session.AccessToken,
                reordered.Select(item => item.Key).ToArray());
            SetRequestStatus(result.IsSuccess, "菜单顺序已更新。", "调整菜单顺序失败。", result.ErrorMessage);
            if (!result.IsSuccess)
            {
                return;
            }

            Items = result.Data?.OrderBy(item => item.Order).ToArray() ?? Array.Empty<PortalMenuItemDto>();
            SelectedItem = Items.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase));
            if (_onMenusChanged != null)
            {
                await _onMenusChanged();
            }
        });
    }

    private async Task ReloadAndNotifyAsync(string selectedKey)
    {
        var result = await ApiClient.GetMenusAsync(Session.AccessToken);
        if (result.IsSuccess && result.Data != null)
        {
            Items = result.Data.OrderBy(item => item.Order).ToArray();
            SelectedItem = Items.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase));
        }

        if (_onMenusChanged != null)
        {
            await _onMenusChanged();
        }
    }

    private void RefreshFilteredItems()
    {
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(HasVisibleItems));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyStateMessage));
        OnPropertyChanged(nameof(MenuOverviewText));
        if (SelectedItem != null && !VisibleItems.Contains(SelectedItem))
        {
            SelectedItem = VisibleItems.FirstOrDefault();
        }
    }

    private static bool MatchesSearch(PortalMenuItemDto item, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var normalizedKeyword = keyword.Trim();
        return item.Title.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
            || item.Key.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
            || item.Route.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
            || item.RequiredPermissions.Any(permission => permission.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyPermissionSelection(IEnumerable<string> permissionNames)
    {
        var selectedNames = permissionNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _isApplyingPermissionSelection = true;
        try
        {
            foreach (var option in PermissionOptions)
            {
                option.SetSelectedSilently(selectedNames.Contains(option.PermissionName));
            }
        }
        finally
        {
            _isApplyingPermissionSelection = false;
        }

        UpdatePermissionSummary();
    }

    private void OnPermissionSelectionChanged()
    {
        if (!_isApplyingPermissionSelection)
        {
            UpdatePermissionSummary();
        }
    }

    private void UpdatePermissionSummary()
    {
        SelectedPermissionCount = PermissionOptions.Count(item => item.IsSelected);
        OnPropertyChanged(nameof(SelectedPermissionSummary));
    }

    private IReadOnlyList<string> GetSelectedPermissions()
    {
        return PermissionOptions
            .Where(item => item.IsSelected)
            .Select(item => item.PermissionName)
            .ToArray();
    }

    private int GetSelectedIndex()
    {
        if (SelectedItem == null)
        {
            return -1;
        }

        for (var index = 0; index < Items.Count; index++)
        {
            if (string.Equals(Items[index].Key, SelectedItem.Key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}

public partial class MenuPermissionOptionViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _suppressSelectionChanged;

    public MenuPermissionOptionViewModel(string permissionName, string displayName, Action selectionChanged)
    {
        PermissionName = permissionName;
        DisplayName = displayName;
        _selectionChanged = selectionChanged;
    }

    public string PermissionName { get; }

    public string DisplayName { get; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_suppressSelectionChanged)
        {
            _selectionChanged();
        }
    }

    public void SetSelectedSilently(bool value)
    {
        if (IsSelected == value)
        {
            return;
        }

        _suppressSelectionChanged = true;
        try
        {
            IsSelected = value;
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
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
    [ObservableProperty] private string _categoryFilter = "全部";
    [ObservableProperty] private string _actionFilter = "全部";
    [ObservableProperty] private string _outcomeFilter = "全部";
    [ObservableProperty] private string _fromDateText = string.Empty;
    [ObservableProperty] private string _toDateText = string.Empty;
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

    public IReadOnlyList<string> CategoryOptions { get; } = ["全部", "认证", "授权", "Token", "权限"];

    public IReadOnlyList<string> ActionOptions { get; } = ["全部", "登录", "登出", "批准授权", "拒绝授权", "签发授权码", "换取访问令牌", "撤销", "创建角色", "修改角色", "删除角色"];

    public IReadOnlyList<string> OutcomeOptions { get; } = ["全部", "成功", "失败"];

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
    private async Task ApplyAuditFiltersAsync()
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

    private async Task LoadPageAsync(int targetPageIndex)
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);
            var currentSelectedId = SelectedItem?.Id;
            DateTime? fromUtc = null;
            DateTime? toUtc = null;
            if (!TryParseDate(FromDateText, out fromUtc) || !TryParseDate(ToDateText, out toUtc))
            {
                StatusMessage = "日期筛选必须是有效日期，例如 2026-01-31。";
                return;
            }

            var result = await ApiClient.GetAuditLogsAsync(
                Session.AccessToken,
                normalizedPageIndex,
                PageSize,
                SearchKeyword,
                CategoryFilter == "全部" ? null : CategoryFilter,
                ActionFilter == "全部" ? null : ActionFilter,
                OutcomeFilter == "全部" ? null : OutcomeFilter,
                fromUtc,
                toUtc);

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

    private static bool TryParseDate(string value, out DateTime? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        if (DateTime.TryParse(value.Trim(), out var parsed))
        {
            result = parsed.Date.ToUniversalTime();
            return true;
        }

        result = null;
        return false;
    }
}
