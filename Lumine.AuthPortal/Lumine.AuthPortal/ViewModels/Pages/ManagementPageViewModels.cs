using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Collections.ObjectModel;

namespace Lumine.AuthPortal.ViewModels.Pages;

public static class PortalUiDefaults
{
    public const int ManagementPageSize = 10;
}

public abstract partial class ManagementPageViewModelBase : ViewModelBase
{
    private const int DefaultPageSize = PortalUiDefaults.ManagementPageSize;

    protected readonly PortalApiClient ApiClient;
    protected readonly PortalSession Session;

    protected ManagementPageViewModelBase(PortalApiClient apiClient, PortalSession session)
    {
        ApiClient = apiClient;
        Session = session;
    }

    [ObservableProperty]
    private string _statusMessage = "准备加载数据。";

    [ObservableProperty]
    private bool _isBusy;

    protected bool EnsureAuthenticated()
    {
        if (Session.IsAuthenticated)
        {
            return true;
        }

        StatusMessage = "请先登录后再访问后台管理页面。";
        return false;
    }

    protected void NotifyProperties(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    protected void NotifyPaginationProperties(bool includeTotalPages = false, params string[] additionalPropertyNames)
    {
        if (includeTotalPages)
        {
            OnPropertyChanged("TotalPages");
        }

        OnPropertyChanged("CanGoPreviousPage");
        OnPropertyChanged("CanGoNextPage");
        OnPropertyChanged("PaginationSummaryText");

        foreach (var propertyName in additionalPropertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    protected bool QueuePageSizeChangeIfNeeded(int selectedPageSize, int currentPageSize, Func<int?, Task> applyPageSizeAsync)
    {
        if (selectedPageSize <= 0 || selectedPageSize == currentPageSize)
        {
            return false;
        }

        _ = applyPageSizeAsync(selectedPageSize);
        return true;
    }

    protected static int NormalizePageSize(int? requestedPageSize, int selectedPageSize)
    {
        var normalizedPageSize = requestedPageSize.GetValueOrDefault(selectedPageSize);
        return normalizedPageSize > 0 ? normalizedPageSize : DefaultPageSize;
    }

    protected static int NormalizePageIndex(int targetPageIndex) => Math.Max(1, targetPageIndex);

    protected static int CalculateLastPageIndex(int nextTotalCount, int pageSize)
    {
        return Math.Max(1, (int)Math.Ceiling(Math.Max(1, nextTotalCount) / (double)pageSize));
    }

    protected static Task LoadCurrentPageAsync(int pageIndex, Func<int, Task> loadPageAsync)
    {
        return loadPageAsync(pageIndex);
    }

    protected static Task SearchFromFirstPageAsync(Func<int, Task> loadPageAsync)
    {
        return loadPageAsync(1);
    }

    protected static Task GoToPreviousPageAsync(bool canGoPreviousPage, int pageIndex, Func<int, Task> loadPageAsync)
    {
        if (!canGoPreviousPage)
        {
            return Task.CompletedTask;
        }

        return loadPageAsync(pageIndex - 1);
    }

    protected static Task GoToNextPageAsync(bool canGoNextPage, int pageIndex, Func<int, Task> loadPageAsync)
    {
        if (!canGoNextPage)
        {
            return Task.CompletedTask;
        }

        return loadPageAsync(pageIndex + 1);
    }

    protected static async Task ApplyPageSizeAndReloadAsync(int? pageSize, int selectedPageSize, Action<int> applyPageSize, Func<int, Task> loadPageAsync)
    {
        var normalizedPageSize = NormalizePageSize(pageSize, selectedPageSize);
        applyPageSize(normalizedPageSize);
        await loadPageAsync(1);
    }

    protected async Task RunBusyActionAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected Task RunAuthenticatedBusyActionAsync(Func<Task> action)
    {
        if (!EnsureAuthenticated())
        {
            return Task.CompletedTask;
        }

        return RunBusyActionAsync(action);
    }

    protected bool TryRequireItem<T>(T? item, string missingMessage, [NotNullWhen(true)] out T? requiredItem)
        where T : class
    {
        requiredItem = item;
        if (requiredItem != null)
        {
            return true;
        }

        StatusMessage = missingMessage;
        return false;
    }

    protected Task RunAuthenticatedBusyActionAsync<T>(T? item, string missingMessage, Func<T, Task> action)
        where T : class
    {
        if (!EnsureAuthenticated() || !TryRequireItem(item, missingMessage, out var requiredItem))
        {
            return Task.CompletedTask;
        }

        return RunBusyActionAsync(() => action(requiredItem));
    }

    protected Task BeginDeleteAsync<T>(T? item, Action<T> prepareDeleteState, Func<T, string> statusMessageFactory)
        where T : class
    {
        if (!EnsureAuthenticated() || item == null)
        {
            return Task.CompletedTask;
        }

        prepareDeleteState(item);
        StatusMessage = statusMessageFactory(item);
        return Task.CompletedTask;
    }

    protected void CancelPendingDelete(Action clearPendingDelete, string statusMessage)
    {
        clearPendingDelete();
        StatusMessage = statusMessage;
    }

    protected void RunUiStateChange(Action stateChange, string? statusMessage = null)
    {
        stateChange();
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            StatusMessage = statusMessage;
        }
    }

    protected void SetRequestStatus(bool isSuccess, string successMessage, string fallbackErrorMessage, string? errorMessage = null)
    {
        StatusMessage = isSuccess ? successMessage : errorMessage ?? fallbackErrorMessage;
    }

    protected static Task ReloadAfterDeleteAsync(int pageIndex, int totalCount, int pageSize, Func<int, Task> loadPageAsync)
    {
        var targetPageIndex = pageIndex;
        var nextTotalCount = Math.Max(0, totalCount - 1);
        var lastPageIndex = CalculateLastPageIndex(nextTotalCount, pageSize);
        if (targetPageIndex > lastPageIndex)
        {
            targetPageIndex = lastPageIndex;
        }

        return loadPageAsync(targetPageIndex);
    }

    protected static TItem? FindItemById<TItem>(IEnumerable<TItem> items, Guid? itemId, Func<TItem, Guid> idSelector)
        where TItem : class
    {
        if (!itemId.HasValue)
        {
            return null;
        }

        return items.FirstOrDefault(item => idSelector(item) == itemId.Value);
    }

    protected static async Task ReloadAndReselectAsync<TItem>(
        Guid? targetItemId,
        Func<Task> reloadAsync,
        Func<IEnumerable<TItem>> itemsProvider,
        Func<TItem, Guid> idSelector,
        Action<TItem> onItemReselected)
        where TItem : class
    {
        await reloadAsync();

        var refreshedItem = FindItemById(itemsProvider(), targetItemId, idSelector);
        if (refreshedItem != null)
        {
            onItemReselected(refreshedItem);
        }
    }
}

public sealed record PermissionReferenceSection(string Title, IReadOnlyList<string> Lines)
{
    public string CopyText => string.Join(Environment.NewLine, Lines);
}

public partial class UsersManagementPageViewModel : ManagementPageViewModelBase
{
    public UsersManagementPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        CreateNew();

        if (session.IsAuthenticated)
        {
            _ = LoadAsync();
        }
    }

    [ObservableProperty] private IReadOnlyList<UserDto> _items = Array.Empty<UserDto>();
    [ObservableProperty] private IReadOnlyList<UserRoleOptionViewModel> _availableRoles = Array.Empty<UserRoleOptionViewModel>();
    [ObservableProperty] private UserDto? _selectedItem;
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _formTitle = "新增用户";
    [ObservableProperty] private string _primaryActionText = "新增用户";
    [ObservableProperty] private bool _isCreateMode = true;
    [ObservableProperty] private bool _isResetPasswordMode;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private UserDto? _pendingDeleteUser;
    [ObservableProperty] private bool _isCreateDialogOpen;
    [ObservableProperty] private bool _isEditorDialogOpen;
    [ObservableProperty] private bool _isSearchVisible;

    partial void OnItemsChanged(IReadOnlyList<UserDto> value)
    {
        OnPropertyChanged(nameof(HasUsers));
        OnPropertyChanged(nameof(ShowUsersEmptyState));
    }

    partial void OnSelectedItemChanged(UserDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedUser));
        OnPropertyChanged(nameof(ShowEditorPlaceholder));
        OnPropertyChanged(nameof(CanSaveUser));
        OnPropertyChanged(nameof(CanResetPassword));
        OnPropertyChanged(nameof(IsIdentityEditable));
        OnPropertyChanged(nameof(IsStatusEditable));
        OnPropertyChanged(nameof(ShowSaveUserButton));
        OnPropertyChanged(nameof(ShowResetPasswordButton));
        OnPropertyChanged(nameof(SelectedUserIdText));
    }

    public bool HasSelectedUser => SelectedItem != null;

    public bool HasUsers => Items.Count > 0;

    public bool ShowUsersEmptyState => !HasUsers;

    public bool ShowEditorPlaceholder => SelectedItem == null;

    public bool IsUserDialogOpen => IsCreateDialogOpen || IsEditorDialogOpen;

    public bool CanSaveUser => !IsResetPasswordMode;

    public bool CanResetPassword => IsResetPasswordMode && SelectedItem != null;

    public bool IsIdentityEditable => !IsResetPasswordMode;

    public bool IsStatusEditable => !IsResetPasswordMode;

    public bool ShowSaveUserButton => !IsResetPasswordMode;

    public bool ShowResetPasswordButton => IsResetPasswordMode && SelectedItem != null;

    public string SelectedUserIdText => SelectedItem?.Id.ToString() ?? "待创建";

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条";

    public bool HasSearchKeyword => !string.IsNullOrWhiteSpace(SearchKeyword);

    public StreamGeometry RefreshIconData => NavigationIconData.Get("refresh");

    public StreamGeometry AddIconData => NavigationIconData.Get("add");

    public StreamGeometry DeleteIconData => NavigationIconData.Get("trash");

    public StreamGeometry SearchIconData => NavigationIconData.Get(IsSearchVisible ? "close" : "search");

    public StreamGeometry ClearSearchIconData => NavigationIconData.Get("close");

    public StreamGeometry PreviousPageIconData => NavigationIconData.Get("chevron-left");

    public StreamGeometry NextPageIconData => NavigationIconData.Get("chevron-right");

    public StreamGeometry EditIconData => NavigationIconData.Get("edit");

    public StreamGeometry AssignRolesIconData => NavigationIconData.Get("roles");

    public StreamGeometry ResetPasswordIconData => NavigationIconData.Get("reset");

    public StreamGeometry ToggleStatusIconData => NavigationIconData.Get("toggle");

    public StreamGeometry ConfirmDeleteIconData => NavigationIconData.Get("trash");

    public StreamGeometry CancelIconData => NavigationIconData.Get("close");

    public StreamGeometry SaveIconData => NavigationIconData.Get("save");

    public bool HasPendingDelete => PendingDeleteUser != null;

    public string DeleteConfirmationText => PendingDeleteUser == null
        ? ""
        : $"确认删除用户“{PendingDeleteUser.UserName}”吗？该操作不可撤销。";

    partial void OnPendingDeleteUserChanged(UserDto? value)
    {
        OnPropertyChanged(nameof(HasPendingDelete));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    partial void OnIsCreateDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUserDialogOpen));
    }

    partial void OnIsEditorDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUserDialogOpen));
    }

    partial void OnPageIndexChanged(int value)
    {
        NotifyPaginationProperties();
    }

    partial void OnPageSizeChanged(int value)
    {
        NotifyPaginationProperties(includeTotalPages: true);
    }

    partial void OnSearchKeywordChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchKeyword));
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

    private async Task LoadPageAsync(int targetPageIndex)
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            PendingDeleteUser = null;
            var currentSelectedId = SelectedItem?.Id;
            var selectedRoleIds = GetSelectedRoleIds();
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);

            var usersTask = ApiClient.GetUsersAsync(Session.AccessToken, normalizedPageIndex, PageSize, SearchKeyword);
            var rolesTask = ApiClient.GetRolesAsync(Session.AccessToken, 1, 100);
            await Task.WhenAll(usersTask, rolesTask);

            var usersResult = usersTask.Result;
            var rolesResult = rolesTask.Result;

            Items = usersResult.Data?.Items ?? Array.Empty<UserDto>();
            TotalCount = usersResult.Data?.TotalCount ?? 0;
            PageIndex = usersResult.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = usersResult.Data?.PageSize ?? PageSize;
            AvailableRoles = (rolesResult.Data?.Items ?? Array.Empty<RoleDto>())
                .OrderBy(role => role.Name)
                .Select(role => new UserRoleOptionViewModel(role.Id, role.Name))
                .ToArray();

            ApplyRoleSelection(selectedRoleIds);

            var refreshedUser = FindItemById(Items, currentSelectedId, item => item.Id);
            if (refreshedUser != null && !IsCreateMode)
            {
                SelectedItem = refreshedUser;
                PopulateForm(refreshedUser, IsResetPasswordMode);
            }

            StatusMessage = usersResult.IsSuccess && rolesResult.IsSuccess
                ? $"已加载第 {PageIndex} 页，共 {TotalCount} 个用户，角色 {AvailableRoles.Count} 个。"
                : usersResult.ErrorMessage ?? rolesResult.ErrorMessage ?? "加载用户管理数据失败。";
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSaveUser) return;

        if (!ValidateUserForm(requirePassword: SelectedItem == null))
        {
            return;
        }

        var roleIds = GetSelectedRoleIds();
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            ApiResult<UserDto> result;
            if (SelectedItem == null)
            {
                result = await ApiClient.CreateUserAsync(Session.AccessToken, new CreateUserRequestDto(UserName.Trim(), Email.Trim(), Password, null, null, IsActive, roleIds));
            }
            else
            {
                result = await ApiClient.UpdateUserAsync(Session.AccessToken, SelectedItem.Id, new UpdateUserRequestDto(UserName.Trim(), Email.Trim(), null, null, IsActive, EmptyToNull(Password)));
                if (result.IsSuccess)
                {
                    var rolesResult = await ApiClient.ReplaceUserRolesAsync(Session.AccessToken, SelectedItem.Id, new ReplaceUserRolesRequestDto(roleIds));
                    result = rolesResult.IsSuccess ? rolesResult : ApiResult<UserDto>.Failure(rolesResult.ErrorMessage ?? "分配角色失败。", rolesResult.StatusCode);
                }
            }

            SetRequestStatus(result.IsSuccess, "用户保存成功。", "保存失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                if (SelectedItem == null)
                {
                    IsCreateDialogOpen = false;
                }
                else
                {
                    IsEditorDialogOpen = false;
                }

                var targetUserId = result.Data?.Id;
                await ReloadAndReselectAsync(targetUserId, LoadAsync, () => Items, item => item.Id, refreshedUser =>
                {
                    SelectedItem = refreshedUser;
                    PopulateForm(refreshedUser, false);
                });
            }
        });
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        RunUiStateChange(() =>
        {
            CreateNew();
            IsCreateDialogOpen = true;
        }, "请填写新增用户信息。");
    }

    [RelayCommand]
    private void CloseCreateDialog()
    {
        RunUiStateChange(() => IsCreateDialogOpen = false, "已关闭新增用户弹窗。");
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
    private void CloseEditorDialog()
    {
        RunUiStateChange(() => IsEditorDialogOpen = false, "已关闭用户编辑弹窗。");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await BeginDeleteAsync(SelectedItem, user => PendingDeleteUser = user, user => $"待删除用户：{user.UserName}");
    }

    [RelayCommand]
    private Task DeleteUserAsync(UserDto? user)
    {
        return BeginDeleteAsync(user, pendingUser => PendingDeleteUser = pendingUser, pendingUser => $"请确认是否删除用户：{pendingUser.UserName}");
    }

    [RelayCommand]
    private void CancelDelete()
    {
        CancelPendingDelete(() => PendingDeleteUser = null, "已取消删除操作。");
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (!EnsureAuthenticated() || PendingDeleteUser == null)
        {
            return;
        }

        var user = PendingDeleteUser;
        await RunBusyActionAsync(async () =>
        {
            var result = await ApiClient.DeleteUserAsync(Session.AccessToken, user.Id);
            SetRequestStatus(result.IsSuccess, "用户已删除。", "删除失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                PendingDeleteUser = null;
                CreateNew();
                await ReloadAfterDeleteAsync(PageIndex, TotalCount, PageSize, LoadPageAsync);
            }
        });
    }

    [RelayCommand]
    private void CreateNew()
    {
        PendingDeleteUser = null;
        IsCreateDialogOpen = false;
        IsEditorDialogOpen = false;
        SelectedItem = null;
        UserName = Email = Password = ConfirmPassword = string.Empty;
        IsActive = true;
        UpdateFormMode(isCreateMode: true, isResetPasswordMode: false, formTitle: "新增用户", primaryActionText: "新增用户");
        ApplyRoleSelection([]);
        StatusMessage = "已切换到新建用户模式。";
    }

    [RelayCommand]
    private void EditUser(UserDto? user)
    {
        OpenUserEditor(user, resetPasswordMode: false, statusMessage: user == null ? string.Empty : $"正在编辑用户：{user.UserName}");
    }

    [RelayCommand]
    private void AssignRoles(UserDto? user)
    {
        OpenUserEditor(user, resetPasswordMode: false, formTitle: "分配角色", primaryActionText: "保存角色", statusMessage: user == null ? string.Empty : $"正在为用户 {user.UserName} 分配角色。");
    }

    [RelayCommand]
    private void PrepareResetPassword(UserDto? user)
    {
        OpenUserEditor(user, resetPasswordMode: true, statusMessage: user == null ? string.Empty : $"正在为用户 {user.UserName} 重置密码。");
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (!EnsureAuthenticated() || SelectedItem == null)
        {
            return;
        }

        if (!ValidatePasswordInputs(requirePassword: true))
        {
            return;
        }

        IsBusy = true;
        var result = await ApiClient.ResetUserPasswordAsync(Session.AccessToken, SelectedItem.Id, new ResetUserPasswordRequestDto(Password));
        SetRequestStatus(result.IsSuccess, "密码已重置。", "重置密码失败。", result.ErrorMessage);
        if (result.IsSuccess)
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            IsEditorDialogOpen = false;
            await ReloadAndReselectAsync(SelectedItem.Id, LoadAsync, () => Items, item => item.Id, refreshedUser =>
            {
                SelectedItem = refreshedUser;
                PopulateForm(refreshedUser, false);
            });
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ToggleUserStatusAsync(UserDto? user)
    {
        if (!EnsureAuthenticated() || user == null)
        {
            return;
        }

        IsBusy = true;
        var nextStatus = !user.IsActive;
        var result = await ApiClient.UpdateUserStatusAsync(Session.AccessToken, user.Id, new UpdateUserStatusRequestDto(nextStatus));
        SetRequestStatus(result.IsSuccess, $"用户已{(nextStatus ? "启用" : "禁用")}。", "更新用户状态失败。", result.ErrorMessage);

        if (result.IsSuccess)
        {
            await ReloadAndReselectAsync(user.Id, LoadAsync, () => Items, item => item.Id, refreshedUser =>
            {
                SelectedItem = refreshedUser;
                PopulateForm(refreshedUser, false);
            });
        }

        IsBusy = false;
    }

    internal static List<Guid> ParseGuidList(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? []
            : input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .Distinct()
                .ToList();
    }

    private void PopulateForm(UserDto user, bool resetPasswordMode)
    {
        UserName = user.UserName;
        Email = user.Email;
        IsActive = user.IsActive;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ApplyRoleSelection(user.Roles.Select(role => role.Id));
        UpdateFormMode(
            isCreateMode: false,
            isResetPasswordMode: resetPasswordMode,
            formTitle: resetPasswordMode ? "重置密码" : "编辑用户",
            primaryActionText: resetPasswordMode ? "重置密码" : "保存变更");
    }

    private void OpenUserEditor(UserDto? user, bool resetPasswordMode, string statusMessage, string? formTitle = null, string? primaryActionText = null)
    {
        if (user == null)
        {
            return;
        }

        PendingDeleteUser = null;
        SelectedItem = user;
        PopulateForm(user, resetPasswordMode);

        if (!string.IsNullOrWhiteSpace(formTitle))
        {
            FormTitle = formTitle;
        }

        if (!string.IsNullOrWhiteSpace(primaryActionText))
        {
            PrimaryActionText = primaryActionText;
        }

        IsEditorDialogOpen = true;
        StatusMessage = statusMessage;
    }

    private void UpdateFormMode(bool isCreateMode, bool isResetPasswordMode, string formTitle, string primaryActionText)
    {
        IsCreateMode = isCreateMode;
        IsResetPasswordMode = isResetPasswordMode;
        FormTitle = formTitle;
        PrimaryActionText = primaryActionText;
        OnPropertyChanged(nameof(CanSaveUser));
        OnPropertyChanged(nameof(CanResetPassword));
        OnPropertyChanged(nameof(IsIdentityEditable));
        OnPropertyChanged(nameof(IsStatusEditable));
        OnPropertyChanged(nameof(ShowSaveUserButton));
        OnPropertyChanged(nameof(ShowResetPasswordButton));
    }

    private List<Guid> GetSelectedRoleIds()
    {
        return AvailableRoles
            .Where(role => role.IsSelected)
            .Select(role => role.Id)
            .ToList();
    }

    private void ApplyRoleSelection(IEnumerable<Guid> roleIds)
    {
        var selectedRoleIds = roleIds.ToHashSet();
        foreach (var role in AvailableRoles)
        {
            role.IsSelected = selectedRoleIds.Contains(role.Id);
        }
    }

    private bool ValidateUserForm(bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            StatusMessage = "用户名不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            StatusMessage = "邮箱不能为空。";
            return false;
        }

        if (!IsValidEmail(Email))
        {
            StatusMessage = "请输入有效的邮箱地址。";
            return false;
        }

        return ValidatePasswordInputs(requirePassword);
    }

    private bool ValidatePasswordInputs(bool requirePassword)
    {
        var hasAnyPasswordInput = !string.IsNullOrWhiteSpace(Password) || !string.IsNullOrWhiteSpace(ConfirmPassword);
        if (!requirePassword && !hasAnyPasswordInput)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            StatusMessage = "请完整输入密码和确认密码。";
            return false;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            StatusMessage = "两次输入的密码不一致。";
            return false;
        }

        return true;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public partial class UserRoleOptionViewModel : ObservableObject
{
    public UserRoleOptionViewModel(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string DisplayName => Name;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class RolesManagementPageViewModel : ManagementPageViewModelBase
{
    private bool _isSynchronizingRoleSelection;
    private bool _isApplyingPermissionSelection;

    public RolesManagementPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
    }

    [ObservableProperty] private IReadOnlyList<RoleDto> _items = Array.Empty<RoleDto>();
    [ObservableProperty] private IReadOnlyList<RoleTableRowViewModel> _roleRows = Array.Empty<RoleTableRowViewModel>();
    [ObservableProperty] private RoleDto? _selectedItem;
    [ObservableProperty] private RoleTableRowViewModel? _selectedRoleRow;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _permissionIdsInput = string.Empty;
    [ObservableProperty] private IReadOnlyList<PermissionDto> _availablePermissions = Array.Empty<PermissionDto>();
    [ObservableProperty] private IReadOnlyList<RolePermissionGroupNodeViewModel> _permissionGroups = Array.Empty<RolePermissionGroupNodeViewModel>();
    [ObservableProperty] private int _selectedPermissionCount;
    [ObservableProperty] private string _selectedPermissionSummary = "未分配权限。";
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _selectedPageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private RoleDto? _pendingDeleteRole;
    [ObservableProperty] private bool _isRoleDialogOpen;

    public bool HasSelectedRole => SelectedItem != null;

    public string CurrentRoleIdentity => SelectedItem?.Id.ToString() ?? "新建角色";

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50, 100];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条";

    public bool HasPendingDelete => PendingDeleteRole != null;

    public string RoleDialogTitle => SelectedItem == null ? "新增角色" : FormattedRoleDialogTitle();

    public string DeleteConfirmationText => PendingDeleteRole == null
        ? string.Empty
        : $"确认删除角色“{PendingDeleteRole.Name}”吗？该角色关联将一并移除。";

    partial void OnSelectedRoleRowChanged(RoleTableRowViewModel? value)
    {
        if (_isSynchronizingRoleSelection)
        {
            return;
        }

        _isSynchronizingRoleSelection = true;
        SelectedItem = value?.Source;
        _isSynchronizingRoleSelection = false;
    }

    partial void OnSelectedItemChanged(RoleDto? value)
    {
        if (!_isSynchronizingRoleSelection)
        {
            _isSynchronizingRoleSelection = true;
            SelectedRoleRow = value == null
                ? null
                : RoleRows.FirstOrDefault(item => item.Id == value.Id);
            _isSynchronizingRoleSelection = false;
        }

        if (value == null)
        {
            Name = string.Empty;
            ApplySelectedPermissions([]);
        }
        else
        {
            Name = value.Name;
            ApplySelectedPermissions(value.Permissions.Select(item => item.Id));
        }

        OnPropertyChanged(nameof(HasSelectedRole));
        OnPropertyChanged(nameof(CurrentRoleIdentity));
        OnPropertyChanged(nameof(RoleDialogTitle));
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

    partial void OnPendingDeleteRoleChanged(RoleDto? value)
    {
        OnPropertyChanged(nameof(HasPendingDelete));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    partial void OnIsRoleDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(RoleDialogTitle));
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
            PendingDeleteRole = null;
            var currentSelectedId = SelectedItem?.Id;
            var selectedPermissionIds = GetSelectedPermissionIds();
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);

            var rolesTask = ApiClient.GetRolesAsync(Session.AccessToken, normalizedPageIndex, PageSize, SearchKeyword);
            var permissionsTask = ApiClient.GetPermissionsAsync(Session.AccessToken, 1, 200);
            await Task.WhenAll(rolesTask, permissionsTask);

            var rolesResult = await rolesTask;
            var permissionsResult = await permissionsTask;

            Items = rolesResult.Data?.Items ?? Array.Empty<RoleDto>();
            TotalCount = rolesResult.Data?.TotalCount ?? 0;
            PageIndex = rolesResult.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = rolesResult.Data?.PageSize ?? PageSize;
            SelectedPageSize = PageSize;
            RoleRows = BuildRoleRows(Items);

            AvailablePermissions = permissionsResult.Data?.Items ?? Array.Empty<PermissionDto>();
            PermissionGroups = BuildPermissionGroups(AvailablePermissions, this);

            SelectedItem = FindItemById(Items, currentSelectedId, item => item.Id);

            if (SelectedItem == null)
            {
                ApplySelectedPermissions(selectedPermissionIds);
            }
            else
            {
                ApplySelectedPermissions(SelectedItem.Permissions.Select(item => item.Id));
            }

            StatusMessage = rolesResult.IsSuccess && permissionsResult.IsSuccess
                ? $"已加载第 {PageIndex} 页，共 {TotalCount} 个角色、{AvailablePermissions.Count} 项权限。"
                : rolesResult.ErrorMessage ?? permissionsResult.ErrorMessage ?? "加载角色管理数据失败。";
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "请输入角色名称。";
            return;
        }

        var permissionIds = GetSelectedPermissionIds();
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            ApiResult<RoleDto> result;
            if (SelectedItem == null)
            {
                result = await ApiClient.CreateRoleAsync(Session.AccessToken, new CreateRoleRequestDto(Name.Trim(), permissionIds));
            }
            else
            {
                result = await ApiClient.UpdateRoleAsync(Session.AccessToken, SelectedItem.Id, new UpdateRoleRequestDto(Name.Trim()));
                if (result.IsSuccess)
                {
                    await ApiClient.ReplaceRolePermissionsAsync(Session.AccessToken, SelectedItem.Id, new ReplaceRolePermissionsRequestDto(permissionIds));
                }
            }

            SetRequestStatus(result.IsSuccess, "角色保存成功。", "保存角色失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                IsRoleDialogOpen = false;
            }

            await ReloadAndReselectAsync(result.Data?.Id, LoadAsync, () => Items, item => item.Id, refreshedRole =>
            {
                SelectedItem = refreshedRole;
            });
        });
    }

    [RelayCommand]
    private async Task AssignPermissionsAsync()
    {
        await RunAuthenticatedBusyActionAsync(SelectedItem, "请先在左侧选择一个角色，再分配权限。", async selectedRole =>
        {
            var result = await ApiClient.ReplaceRolePermissionsAsync(
                Session.AccessToken,
                selectedRole.Id,
                new ReplaceRolePermissionsRequestDto(GetSelectedPermissionIds()));

            SetRequestStatus(result.IsSuccess, "角色权限分配成功。", "分配角色权限失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                IsRoleDialogOpen = false;
            }

            await LoadAsync();
        });
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        RunUiStateChange(() =>
        {
            CreateNew();
            IsRoleDialogOpen = true;
        }, "请填写角色信息并分配权限。");
    }

    [RelayCommand]
    private void OpenEditDialog()
    {
        if (!TryRequireItem(SelectedItem, "请先选择一个角色。", out var selectedRole))
        {
            return;
        }

        IsRoleDialogOpen = true;
        StatusMessage = $"正在编辑角色：{selectedRole.Name}";
    }

    [RelayCommand]
    private void OpenAssignPermissionsDialog()
    {
        if (!TryRequireItem(SelectedItem, "请先选择一个角色，再分配权限。", out var selectedRole))
        {
            return;
        }

        IsRoleDialogOpen = true;
        StatusMessage = $"正在为角色 {selectedRole.Name} 分配权限。";
    }

    [RelayCommand]
    private void CloseRoleDialog()
    {
        RunUiStateChange(() => IsRoleDialogOpen = false, "已关闭角色编辑弹窗。");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await BeginDeleteAsync(SelectedItem, role => PendingDeleteRole = role, role => $"待删除角色：{role.Name}");
    }

    [RelayCommand]
    private void DeleteRole(RoleDto? role)
    {
        _ = BeginDeleteAsync(role, pendingRole =>
        {
            PendingDeleteRole = pendingRole;
            SelectedItem = pendingRole;
        }, pendingRole => $"请确认是否删除角色：{pendingRole.Name}");
    }

    [RelayCommand]
    private void CancelDelete()
    {
        CancelPendingDelete(() => PendingDeleteRole = null, "已取消删除角色。");
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (!EnsureAuthenticated() || PendingDeleteRole == null)
        {
            return;
        }

        var role = PendingDeleteRole;
        await RunBusyActionAsync(async () =>
        {
            var result = await ApiClient.DeleteRoleAsync(Session.AccessToken, role.Id);
            SetRequestStatus(result.IsSuccess, "角色已删除。", "删除角色失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                PendingDeleteRole = null;
                SelectedItem = null;
                await ReloadAfterDeleteAsync(PageIndex, TotalCount, PageSize, LoadPageAsync);
            }
        });
    }

    [RelayCommand]
    private void CreateNew()
    {
        PendingDeleteRole = null;
        IsRoleDialogOpen = false;
        SelectedItem = null;
        SelectedRoleRow = null;
        Name = string.Empty;
        ApplySelectedPermissions([]);
        StatusMessage = "已切换到新建角色模式。";
    }

    internal void OnPermissionGroupCheckChanged(RolePermissionGroupNodeViewModel group, bool? isChecked)
    {
        if (_isApplyingPermissionSelection)
        {
            return;
        }

        if (isChecked is null)
        {
            group.RefreshCheckState();
            SyncPermissionSummary();
            return;
        }

        _isApplyingPermissionSelection = true;
        try
        {
            foreach (var item in group.Items)
            {
                item.SetCheckedSilently(isChecked.Value);
            }
        }
        finally
        {
            _isApplyingPermissionSelection = false;
        }

        group.RefreshCheckState();
        SyncPermissionSummary();
    }

    internal void OnPermissionLeafCheckChanged(RolePermissionLeafNodeViewModel item)
    {
        if (_isApplyingPermissionSelection)
        {
            return;
        }

        item.Group?.RefreshCheckState();
        SyncPermissionSummary();
    }

    private void ApplySelectedPermissions(IEnumerable<Guid> permissionIds)
    {
        var selectedIds = permissionIds.ToHashSet();

        _isApplyingPermissionSelection = true;
        try
        {
            foreach (var group in PermissionGroups)
            {
                foreach (var item in group.Items)
                {
                    item.SetCheckedSilently(selectedIds.Contains(item.Id));
                }

                group.RefreshCheckState();
            }
        }
        finally
        {
            _isApplyingPermissionSelection = false;
        }

        SyncPermissionSummary();
    }

    private List<Guid> GetSelectedPermissionIds()
    {
        return PermissionGroups
            .SelectMany(group => group.Items)
            .Where(item => item.IsChecked)
            .Select(item => item.Id)
            .Distinct()
            .ToList();
    }

    private void SyncPermissionSummary()
    {
        var selectedItems = PermissionGroups
            .SelectMany(group => group.Items)
            .Where(item => item.IsChecked)
            .OrderBy(item => item.GroupTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PermissionIdsInput = string.Join(',', selectedItems.Select(item => item.Id));
        SelectedPermissionCount = selectedItems.Count;
        SelectedPermissionSummary = selectedItems.Count == 0
            ? "未分配权限。"
            : string.Join("、", selectedItems.Select(item => item.DisplayName));

        foreach (var group in PermissionGroups)
        {
            group.NotifySummaryChanged();
        }
    }

    private static IReadOnlyList<RoleTableRowViewModel> BuildRoleRows(IReadOnlyList<RoleDto> roles)
    {
        return roles
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new RoleTableRowViewModel(
                item.Id,
                item,
                item.Name,
                BuildRoleDescription(item),
                item.Permissions.Count,
                "--"))
            .ToList();
    }

    private static string BuildRoleDescription(RoleDto role)
    {
        if (role.Permissions.Count == 0)
        {
            return "未分配权限";
        }

        var labels = role.Permissions
            .Select(item => item.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (role.Permissions.Count <= 2)
        {
            return string.Join("、", labels);
        }

        return $"{string.Join("、", labels)} 等 {role.Permissions.Count} 项";
    }

    private static IReadOnlyList<RolePermissionGroupNodeViewModel> BuildPermissionGroups(
        IReadOnlyList<PermissionDto> permissions,
        RolesManagementPageViewModel owner)
    {
        return permissions
            .GroupBy(item => GetPermissionGroupKey(item.PermissionName), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetPermissionGroupDisplayName(group.Key), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var items = group
                    .OrderBy(item => GetPermissionActionDisplayName(item.PermissionName), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new RolePermissionLeafNodeViewModel(
                        item.Id,
                        item.PermissionName,
                        GetPermissionActionDisplayName(item.PermissionName),
                        item.DisplayName,
                        GetPermissionGroupDisplayName(group.Key)))
                    .ToList();

                var node = new RolePermissionGroupNodeViewModel(
                    group.Key,
                    GetPermissionGroupDisplayName(group.Key),
                    GetPermissionGroupHint(group.Key),
                    items)
                {
                    Owner = owner
                };

                foreach (var item in items)
                {
                    item.Owner = owner;
                    item.Group = node;
                }

                node.RefreshCheckState();
                return node;
            })
            .ToList();
    }

    private static string GetPermissionGroupKey(string permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return "common";
        }

        var segments = permissionName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? "common" : segments[0];
    }

    private static string GetPermissionGroupDisplayName(string groupKey)
    {
        return groupKey.ToLowerInvariant() switch
        {
            "users" => "用户管理",
            "roles" => "角色管理",
            "permissions" => "权限管理",
            "clients" => "客户端管理",
            "auditlogs" => "审计日志",
            "system" => "系统管理",
            _ => groupKey
        };
    }

    private static string GetPermissionGroupHint(string groupKey)
    {
        return groupKey.ToLowerInvariant() switch
        {
            "users" => "用户的查看、编辑与授权能力",
            "roles" => "角色的新增、编辑与分配能力",
            "permissions" => "权限字典的维护能力",
            "clients" => "OIDC 客户端配置能力",
            "auditlogs" => "审计日志查看能力",
            "system" => "系统级菜单与通用能力",
            _ => "按模块分组展示权限"
        };
    }

    private static string GetPermissionActionDisplayName(string permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return "未命名权限";
        }

        var segments = permissionName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var action = segments.Length <= 1 ? permissionName : segments[^1];

        return action.ToLowerInvariant() switch
        {
            "view" => "查看",
            "manage" => "管理",
            "create" => "新增",
            "update" => "编辑",
            "delete" => "删除",
            _ => action
        };
    }

    private string FormattedRoleDialogTitle()
    {
        return SelectedItem == null ? "新增角色" : $"编辑角色 · {SelectedItem.Name}";
    }
}

public sealed record RoleTableRowViewModel(
    Guid Id,
    RoleDto Source,
    string Name,
    string Description,
    int PermissionCount,
    string CreatedTimeText);

public sealed partial class RolePermissionGroupNodeViewModel : ObservableObject
{
    private bool? _isChecked;

    public RolePermissionGroupNodeViewModel(
        string key,
        string title,
        string hint,
        IReadOnlyList<RolePermissionLeafNodeViewModel> items)
    {
        Key = key;
        Title = title;
        Hint = hint;
        Items = items;
    }

    public string Key { get; }

    public string Title { get; }

    public string Hint { get; }

    public IReadOnlyList<RolePermissionLeafNodeViewModel> Items { get; }

    public RolesManagementPageViewModel? Owner { get; internal set; }

    public string Summary => $"已选 {Items.Count(item => item.IsChecked)} / {Items.Count}";

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            SetProperty(ref _isChecked, value);
            Owner?.OnPermissionGroupCheckChanged(this, value);
        }
    }

    internal void RefreshCheckState()
    {
        var checkedCount = Items.Count(item => item.IsChecked);
        bool? state = checkedCount == 0
            ? false
            : checkedCount == Items.Count
                ? true
                : null;

        SetProperty(ref _isChecked, state, nameof(IsChecked));
        OnPropertyChanged(nameof(Summary));
    }

    internal void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(Summary));
    }
}

public sealed partial class RolePermissionLeafNodeViewModel : ObservableObject
{
    private bool _isChecked;

    public RolePermissionLeafNodeViewModel(
        Guid id,
        string permissionName,
        string title,
        string displayName,
        string groupTitle)
    {
        Id = id;
        PermissionName = permissionName;
        Title = title;
        DisplayName = displayName;
        GroupTitle = groupTitle;
    }

    public Guid Id { get; }

    public string PermissionName { get; }

    public string Title { get; }

    public string DisplayName { get; }

    public string GroupTitle { get; }

    public RolesManagementPageViewModel? Owner { get; internal set; }

    public RolePermissionGroupNodeViewModel? Group { get; internal set; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                Owner?.OnPermissionLeafCheckChanged(this);
            }
        }
    }

    internal void SetCheckedSilently(bool value)
    {
        SetProperty(ref _isChecked, value, nameof(IsChecked));
    }
}

public partial class PermissionsManagementPageViewModel : ManagementPageViewModelBase
{
    public PermissionsManagementPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        ReferenceSections =
        [
            new PermissionReferenceSection("菜单", ["身份管理", "└ 权限管理"]),
            new PermissionReferenceSection("权限其实是：", ["API 权限", "页面权限", "菜单权限"]),
            new PermissionReferenceSection("表", ["ID", "权限名称", "权限编码", "类型", "描述"]),
            new PermissionReferenceSection("类型", ["menu", "api", "page"]),
            new PermissionReferenceSection("示例", ["user.read", "user.write", "role.manage", "audit.view"])
        ];
    }

    [ObservableProperty] private IReadOnlyList<PermissionDto> _items = Array.Empty<PermissionDto>();
    [ObservableProperty] private PermissionDto? _selectedItem;
    [ObservableProperty] private string _permissionName = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _selectedPageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private PermissionDto? _pendingDeletePermission;
    [ObservableProperty] private bool _isPermissionDialogOpen;

    public IReadOnlyList<PermissionReferenceSection> ReferenceSections { get; }

    public string EditorTitle => SelectedItem == null ? "新建权限" : "编辑权限";

    public string PermissionCountText => $"当前页 {Items.Count} 条，累计 {TotalCount} 条权限记录";

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50, 100];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条";

    public bool HasPendingDelete => PendingDeletePermission != null;

    public string PermissionDialogTitle => SelectedItem == null ? "新建权限" : $"编辑权限 · {SelectedItem.PermissionName}";

    public string DeleteConfirmationText => PendingDeletePermission == null
        ? string.Empty
        : $"确认删除权限“{PendingDeletePermission.PermissionName}”吗？";

    partial void OnSelectedItemChanged(PermissionDto? value)
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(PermissionDialogTitle));

        if (value == null)
        {
            return;
        }

        PermissionName = value.PermissionName;
        DisplayName = value.DisplayName;
    }

    partial void OnItemsChanged(IReadOnlyList<PermissionDto> value)
    {
        OnPropertyChanged(nameof(PermissionCountText));
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
        NotifyPaginationProperties(includeTotalPages: true, nameof(PermissionCountText));
    }

    partial void OnPendingDeletePermissionChanged(PermissionDto? value)
    {
        OnPropertyChanged(nameof(HasPendingDelete));
        OnPropertyChanged(nameof(DeleteConfirmationText));
    }

    partial void OnIsPermissionDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(PermissionDialogTitle));
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
            PendingDeletePermission = null;
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);
            var currentSelectedId = SelectedItem?.Id;
            var result = await ApiClient.GetPermissionsAsync(Session.AccessToken, normalizedPageIndex, PageSize, SearchKeyword);
            Items = result.Data?.Items ?? Array.Empty<PermissionDto>();
            TotalCount = result.Data?.TotalCount ?? 0;
            PageIndex = result.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = result.Data?.PageSize ?? PageSize;
            SelectedPageSize = PageSize;
            SelectedItem = FindItemById(Items, currentSelectedId, item => item.Id);

            SetRequestStatus(result.IsSuccess, $"已加载第 {PageIndex} 页，共 {TotalCount} 个权限。", "加载权限失败。", result.ErrorMessage);
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAuthenticatedBusyActionAsync(async () =>
        {
            ApiResult<PermissionDto> result = SelectedItem == null
                ? await ApiClient.CreatePermissionAsync(Session.AccessToken, new CreatePermissionRequestDto(PermissionName, EmptyToNull(DisplayName)))
                : await ApiClient.UpdatePermissionAsync(Session.AccessToken, SelectedItem.Id, new UpdatePermissionRequestDto(PermissionName, EmptyToNull(DisplayName)));

            SetRequestStatus(result.IsSuccess, "权限保存成功。", "保存权限失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                IsPermissionDialogOpen = false;
            }

            await ReloadAndReselectAsync(result.Data?.Id, LoadAsync, () => Items, item => item.Id, refreshedPermission =>
            {
                SelectedItem = refreshedPermission;
            });
        });
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        RunUiStateChange(() =>
        {
            CreateNew();
            IsPermissionDialogOpen = true;
        }, "请填写权限信息。");
    }

    [RelayCommand]
    private void OpenEditDialog()
    {
        if (!TryRequireItem(SelectedItem, "请先选择一个权限。", out var selectedPermission))
        {
            return;
        }

        IsPermissionDialogOpen = true;
        StatusMessage = $"正在编辑权限：{selectedPermission.PermissionName}";
    }

    [RelayCommand]
    private void ClosePermissionDialog()
    {
        RunUiStateChange(() => IsPermissionDialogOpen = false, "已关闭权限编辑弹窗。");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await BeginDeleteAsync(SelectedItem, permission => PendingDeletePermission = permission, permission => $"待删除权限：{permission.PermissionName}");
    }

    [RelayCommand]
    private void DeletePermission(PermissionDto? permission)
    {
        _ = BeginDeleteAsync(permission, pendingPermission =>
        {
            PendingDeletePermission = pendingPermission;
            SelectedItem = pendingPermission;
        }, pendingPermission => $"请确认是否删除权限：{pendingPermission.PermissionName}");
    }

    [RelayCommand]
    private void CancelDelete()
    {
        CancelPendingDelete(() => PendingDeletePermission = null, "已取消删除权限。");
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (!EnsureAuthenticated() || PendingDeletePermission == null)
        {
            return;
        }

        var permission = PendingDeletePermission;
        await RunBusyActionAsync(async () =>
        {
            var result = await ApiClient.DeletePermissionAsync(Session.AccessToken, permission.Id);
            SetRequestStatus(result.IsSuccess, "权限已删除。", "删除权限失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                PendingDeletePermission = null;
                SelectedItem = null;
                await ReloadAfterDeleteAsync(PageIndex, TotalCount, PageSize, LoadPageAsync);
            }
        });
    }

    [RelayCommand]
    private void CreateNew()
    {
        PendingDeletePermission = null;
        IsPermissionDialogOpen = false;
        SelectedItem = null;
        PermissionName = string.Empty;
        DisplayName = string.Empty;
        OnPropertyChanged(nameof(EditorTitle));
        StatusMessage = "已切换到新建权限模式。";
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public partial class ClientsManagementPageViewModel : ManagementPageViewModelBase
{
    private const string DefaultPurpose = "第三方系统接入";
    private const string DefaultScopes = "openid,profile,email,roles,permissions";
    private const string DefaultRedirectUris = "http://localhost:5173/signin-oidc";

    public ClientsManagementPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
    }

    [ObservableProperty] private IReadOnlyList<OidcClientDto> _items = Array.Empty<OidcClientDto>();
    [ObservableProperty] private OidcClientDto? _selectedItem;
    [ObservableProperty] private string _clientId = string.Empty;
    [ObservableProperty] private string _clientName = string.Empty;
    [ObservableProperty] private string _clientType = "public";
    [ObservableProperty] private string _allowedScopesInput = "openid,profile,email,roles,permissions";
    [ObservableProperty] private string _redirectUrisInput = "http://localhost:5173/signin-oidc";
    [ObservableProperty] private bool _requirePkce = true;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _selectedPageSize = PortalUiDefaults.ManagementPageSize;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private OidcClientDto? _pendingDeleteClient;
    [ObservableProperty] private bool _isClientDialogOpen;
    [ObservableProperty] private string _clientDialogTitle = "新建客户端";

    public string SectionTitle => "6 OAuth客户端管理";

    public string SectionSubtitle => "面向第三方系统接入的 OAuth 客户端配置与说明。";

    public string MenuTreeText => "认证授权\n└ OAuth客户端";

    public string UsageText => string.IsNullOrWhiteSpace(Description) ? DefaultPurpose : Description.Trim();

    public string TableSchemaText => string.Join(Environment.NewLine,
    [
        "ClientId",
        "ClientName",
        "ClientSecret",
        "GrantType",
        "RedirectUri",
        "Scope",
        "状态"
    ]);

    public string SupportedTypesText
    {
        get
        {
            var grantTypes = new List<string>
            {
                "authorization_code",
                "client_credentials",
                "password"
            };

            if (RequirePkce)
            {
                grantTypes.Add("pkce");
            }

            return string.Join(Environment.NewLine, grantTypes);
        }
    }

    public string SelectedClientSummaryText => string.Join(Environment.NewLine,
    [
        $"ClientId: {(string.IsNullOrWhiteSpace(ClientId) ? "未设置" : ClientId)}",
        $"ClientName: {(string.IsNullOrWhiteSpace(ClientName) ? "未设置" : ClientName)}",
        $"ClientType: {ClientType}",
        $"RedirectUri: {BuildPreviewLine(RedirectUrisInput, "未配置回调地址")}",
        $"Scope: {BuildPreviewLine(AllowedScopesInput, "未配置 Scope")}",
        $"状态: {(IsActive ? "启用" : "停用")}"
    ]);

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50, 100];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool CanGoPreviousPage => PageIndex > 1;

    public bool CanGoNextPage => PageIndex < TotalPages;

    public string PaginationSummaryText => $"第 {PageIndex} / {TotalPages} 页 · 共 {TotalCount} 条";

    public string ClientCountText => $"当前页 {Items.Count} 条，累计 {TotalCount} 个客户端";

    public bool HasPendingDelete => PendingDeleteClient != null;

    public string DeleteConfirmationText => PendingDeleteClient == null
        ? string.Empty
        : $"确认删除客户端“{PendingDeleteClient.ClientName}”吗？";

    partial void OnSelectedItemChanged(OidcClientDto? value)
    {
        if (value == null) return;
        ClientId = value.ClientId;
        ClientName = value.ClientName;
        ClientType = value.ClientType;
        AllowedScopesInput = string.Join(',', value.AllowedScopes);
        RedirectUrisInput = string.Join(',', value.RedirectUris);
        RequirePkce = value.RequirePkce;
        IsActive = value.IsActive;
        Description = value.Description ?? string.Empty;
        NotifyPresentationChanged();
    }

    partial void OnClientIdChanged(string value) => NotifyPresentationChanged();

    partial void OnClientNameChanged(string value) => NotifyPresentationChanged();

    partial void OnClientTypeChanged(string value) => NotifyPresentationChanged();

    partial void OnAllowedScopesInputChanged(string value) => NotifyPresentationChanged();

    partial void OnRedirectUrisInputChanged(string value) => NotifyPresentationChanged();

    partial void OnRequirePkceChanged(bool value) => NotifyPresentationChanged();

    partial void OnIsActiveChanged(bool value) => NotifyPresentationChanged();

    partial void OnDescriptionChanged(string value) => NotifyPresentationChanged();

    partial void OnItemsChanged(IReadOnlyList<OidcClientDto> value)
    {
        OnPropertyChanged(nameof(ClientCountText));
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
        NotifyPaginationProperties(includeTotalPages: true, nameof(ClientCountText));
    }

    partial void OnPendingDeleteClientChanged(OidcClientDto? value)
    {
        OnPropertyChanged(nameof(HasPendingDelete));
        OnPropertyChanged(nameof(DeleteConfirmationText));
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
            PendingDeleteClient = null;
            var normalizedPageIndex = NormalizePageIndex(targetPageIndex);
            var currentSelectedId = SelectedItem?.Id;
            var result = await ApiClient.GetClientsAsync(Session.AccessToken, normalizedPageIndex, PageSize, SearchKeyword);
            Items = result.Data?.Items ?? Array.Empty<OidcClientDto>();
            TotalCount = result.Data?.TotalCount ?? 0;
            PageIndex = result.Data?.PageIndex ?? normalizedPageIndex;
            PageSize = result.Data?.PageSize ?? PageSize;
            SelectedPageSize = PageSize;
            SelectedItem = FindItemById(Items, currentSelectedId, item => item.Id);

            SetRequestStatus(result.IsSuccess, $"已加载第 {PageIndex} 页，共 {TotalCount} 个客户端。", "加载客户端失败。", result.ErrorMessage);
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var request = new SaveClientRequestDto(
            ClientId,
            ClientName,
            ClientType,
            SplitCsv(AllowedScopesInput),
            SplitCsv(RedirectUrisInput),
            RequirePkce,
            IsActive,
            EmptyToNull(Description));

        await RunAuthenticatedBusyActionAsync(async () =>
        {
            ApiResult<OidcClientDto> result = SelectedItem == null
                ? await ApiClient.CreateClientAsync(Session.AccessToken, request)
                : await ApiClient.UpdateClientAsync(Session.AccessToken, SelectedItem.Id, request);

            SetRequestStatus(result.IsSuccess, "客户端保存成功。", "保存客户端失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                IsClientDialogOpen = false;
            }

            await ReloadAndReselectAsync(result.Data?.Id, LoadAsync, () => Items, item => item.Id, refreshedClient =>
            {
                SelectedItem = refreshedClient;
            });
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await BeginDeleteAsync(SelectedItem, client => PendingDeleteClient = client, client => $"待删除客户端：{client.ClientName}");
    }

    [RelayCommand]
    private void DeleteClient(OidcClientDto? client)
    {
        _ = BeginDeleteAsync(client, pendingClient =>
        {
            PendingDeleteClient = pendingClient;
            SelectedItem = pendingClient;
        }, pendingClient => $"请确认是否删除客户端：{pendingClient.ClientName}");
    }

    [RelayCommand]
    private void CancelDelete()
    {
        CancelPendingDelete(() => PendingDeleteClient = null, "已取消删除客户端。");
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (!EnsureAuthenticated() || PendingDeleteClient == null)
        {
            return;
        }

        var client = PendingDeleteClient;
        await RunBusyActionAsync(async () =>
        {
            var result = await ApiClient.DeleteClientAsync(Session.AccessToken, client.Id);
            SetRequestStatus(result.IsSuccess, "客户端已删除。", "删除客户端失败。", result.ErrorMessage);
            if (result.IsSuccess)
            {
                PendingDeleteClient = null;
                SelectedItem = null;
                await ReloadAfterDeleteAsync(PageIndex, TotalCount, PageSize, LoadPageAsync);
            }
        });
    }

    [RelayCommand]
    private void CreateNew()
    {
        OpenCreateDialog();
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        RunUiStateChange(() =>
        {
            PendingDeleteClient = null;
            SelectedItem = null;
            ClientDialogTitle = "新建客户端";
            ClientId = ClientName = Description = string.Empty;
            ClientType = "public";
            AllowedScopesInput = DefaultScopes;
            RedirectUrisInput = DefaultRedirectUris;
            RequirePkce = true;
            IsActive = true;
            IsClientDialogOpen = true;
            NotifyPresentationChanged();
        }, "已切换到新建客户端模式。");
    }

    [RelayCommand]
    private void OpenEditDialog(OidcClientDto? client = null)
    {
        if (client != null)
        {
            SelectedItem = client;
        }

        if (!TryRequireItem(SelectedItem, "请先选择一个客户端。", out var selectedClient))
        {
            return;
        }

        PendingDeleteClient = null;
        ClientDialogTitle = $"编辑客户端 · {selectedClient.ClientName}";
        IsClientDialogOpen = true;
        StatusMessage = $"正在编辑客户端：{selectedClient.ClientName}";
    }

    [RelayCommand]
    private void CloseClientDialog()
    {
        RunUiStateChange(() => IsClientDialogOpen = false);
    }

    private static List<string> SplitCsv(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? []
            : input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private void NotifyPresentationChanged()
    {
        OnPropertyChanged(nameof(UsageText));
        OnPropertyChanged(nameof(SupportedTypesText));
        OnPropertyChanged(nameof(SelectedClientSummaryText));
    }

    private static string BuildPreviewLine(string? rawValue, string fallback)
    {
        var values = SplitCsv(rawValue);
        return values.Count == 0 ? fallback : string.Join(", ", values);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public partial class AuditLogsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "审计日志";

    [ObservableProperty]
    private string _description = "Step 10 占位：后续可接入登录审计、授权审计、管理操作审计列表。";
}
