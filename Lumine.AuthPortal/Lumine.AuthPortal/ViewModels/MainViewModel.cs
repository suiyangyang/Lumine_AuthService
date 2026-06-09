using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using Lumine.AuthPortal.Services;
using Lumine.AuthPortal.ViewModels.Pages;
using System.Globalization;

namespace Lumine.AuthPortal.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly PortalApiClient _apiClient;
    private readonly PortalSession _session;
    private readonly ThemeService _themeService;
    private readonly Dictionary<string, Func<ViewModelBase>> _pageFactory;
    private readonly Dictionary<string, NavigationItemViewModel> _navigationLookup;

    public MainViewModel(PortalApiClient apiClient, PortalSession session, ThemeService themeService)
    {
        _apiClient = apiClient;
        _session = session;
        _themeService = themeService;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PortalSession.AccessToken)
                or nameof(PortalSession.Permissions)
                or nameof(PortalSession.IsAuthenticated))
            {
                RefreshNavigationVisibility();
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAnonymous));
                OnPropertyChanged(nameof(VisibleNavigationSections));
                OnPropertyChanged(nameof(HasAdminAccess));
            }

            if (args.PropertyName == nameof(PortalSession.IsAuthenticated))
            {
                HandleAuthenticationStateChanged();
            }

            if (args.PropertyName is nameof(PortalSession.AccessToken)
                or nameof(PortalSession.IsAuthenticated)
                or nameof(PortalSession.UserName)
                or nameof(PortalSession.Email)
                or nameof(PortalSession.Roles))
            {
                OnPropertyChanged(nameof(AccountDisplayName));
                OnPropertyChanged(nameof(AccountEmail));
                OnPropertyChanged(nameof(AccountInitial));
                OnPropertyChanged(nameof(AccountRolesSummary));
                OnPropertyChanged(nameof(AccountRoles));
                OnPropertyChanged(nameof(AccountStatusText));
            }
        };

        _themeService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ThemeService.SelectedTheme))
            {
                OnPropertyChanged(nameof(ThemeToggleIconData));
                OnPropertyChanged(nameof(CurrentThemeLabel));
                OnPropertyChanged(nameof(IsFluentThemeSelected));
                OnPropertyChanged(nameof(IsLumineDarkThemeSelected));
            }
        };

        var dashboard = new NavigationItemViewModel("dashboard", "仪表盘", "dashboard");
        var login = new NavigationItemViewModel("login", "登录", "login");
        var register = new NavigationItemViewModel("register", "注册", "register");
        var users = new NavigationItemViewModel("users", "用户管理", "users", "users.view", "users.manage");
        var roles = new NavigationItemViewModel("roles", "角色管理", "roles", "roles.view", "roles.manage");
        var permissions = new NavigationItemViewModel("permissions", "权限管理", "permissions", "permissions.view", "permissions.manage");
        var userGroups = new NavigationItemViewModel("user-groups", "用户组管理", "user-groups", "users.view", "users.manage");
        var clients = new NavigationItemViewModel("clients", "OAuth 客户端", "clients", "clients.view", "clients.manage");
        var consent = new NavigationItemViewModel("consent", "授权确认", "consent");
        var tokens = new NavigationItemViewModel("tokens", "Token 管理", "tokens", "clients.view", "clients.manage");
        var oidc = new NavigationItemViewModel("oidc", "OIDC Discovery", "oidc");
        var menus = new NavigationItemViewModel("menus", "菜单管理", "menus", "permissions.view", "permissions.manage");
        var settings = new NavigationItemViewModel("settings", "系统配置", "settings");
        var audit = new NavigationItemViewModel("audit", "审计日志", "audit", "clients.view", "clients.manage");

        NavigationItems =
        [
            dashboard,
            login,
            register,
            users,
            roles,
            permissions,
            userGroups,
            clients,
            consent,
            tokens,
            oidc,
            menus,
            settings,
            audit
        ];

        NavigationSections =
        [
            new NavigationSectionViewModel("系统概览", "overview", [dashboard]),
            new NavigationSectionViewModel("身份管理", "identity", [users, roles, permissions, userGroups]),
            new NavigationSectionViewModel("认证授权", "auth", [clients, consent, tokens, oidc]),
            new NavigationSectionViewModel("系统管理", "system", [menus, settings, audit])
        ];

        _navigationLookup = NavigationItems.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        _pageFactory = new Dictionary<string, Func<ViewModelBase>>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard"] = () => new DashboardPageViewModel(_apiClient, _session),
            ["login"] = () => CreateLoginPage(),
            ["register"] = () => CreateRegisterPage(),
            ["user-groups"] = () => new UserGroupsManagementPageViewModel(_apiClient, _session),
            ["consent"] = () => new ConsentPageViewModel(_apiClient, _session),
            ["users"] = () => new UsersManagementPageViewModel(_apiClient, _session),
            ["roles"] = () => new RolesManagementPageViewModel(_apiClient, _session),
            ["permissions"] = () => new PermissionsManagementPageViewModel(_apiClient, _session),
            ["clients"] = () => new ClientsManagementPageViewModel(_apiClient, _session),
            ["tokens"] = () => new TokensManagementPageViewModel(_apiClient, _session),
            ["oidc"] = () => new OidcPlaygroundPageViewModel(_apiClient, _session),
            ["menus"] = () => new MenusManagementPageViewModel(_apiClient, _session),
            ["settings"] = () => new SystemSettingsPageViewModel(_apiClient, _session, _themeService),
            ["audit"] = () => new AuditLogsPageViewModel(_apiClient, _session)
        };

        RefreshNavigationVisibility();
        if (IsAuthenticated)
        {
            Navigate("dashboard");
        }
        else
        {
            ShowLoginPage();
        }
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public IReadOnlyList<NavigationSectionViewModel> NavigationSections { get; }

    public IEnumerable<NavigationSectionViewModel> VisibleNavigationSections => NavigationSections
        .Where(section => section.Items.Any(item => item.IsVisible));

    public bool IsAuthenticated => _session.IsAuthenticated;

    public bool IsAnonymous => !IsAuthenticated;

    public bool IsLoginPageSelected => string.Equals(CurrentPageKey, "login", StringComparison.OrdinalIgnoreCase);

    public bool IsRegisterPageSelected => string.Equals(CurrentPageKey, "register", StringComparison.OrdinalIgnoreCase);

    public PortalSession Session => _session;

    public StreamGeometry AccountMenuChevronIconData => NavigationIconData.Get("chevron-down");

    public StreamGeometry ThemeToggleIconData => NavigationIconData.Get(IsFluentThemeSelected ? "moon" : "sun");

    public string CurrentThemeLabel => _themeService.CurrentThemeLabel;

    public bool IsFluentThemeSelected => _themeService.IsFluentThemeSelected;

    public bool IsLumineDarkThemeSelected => _themeService.IsLumineDarkThemeSelected;

    public bool HasAdminAccess => VisibleNavigationSections
        .SelectMany(section => section.Items)
        .Any(item => item.RequiresPermission);

    public string AccountDisplayName => string.IsNullOrWhiteSpace(_session.UserName) ? "访客" : _session.UserName;

    public string AccountEmail => string.IsNullOrWhiteSpace(_session.Email) ? "未配置邮箱" : _session.Email;

    public string AccountStatusText => IsAuthenticated ? "已登录到后台系统" : "未登录";

    public string AccountRolesSummary => _session.Roles.Count switch
    {
        0 => IsAuthenticated ? "当前角色：未分配" : "当前角色：访客",
        _ => $"当前角色：{string.Join("、", _session.Roles)}"
    };

    public IReadOnlyList<string> AccountRoles => _session.Roles.Count == 0
        ? (IsAuthenticated ? ["未分配"] : ["访客"])
        : _session.Roles;

    public string AccountInitial
    {
        get
        {
            var value = AccountDisplayName.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "访";
            }

            var first = StringInfo.GetNextTextElement(value, 0);
            return string.IsNullOrWhiteSpace(first) ? "访" : first.ToUpperInvariant();
        }
    }

    [ObservableProperty]
    private ViewModelBase _currentPage = new PlaceholderPageViewModel("初始化中", "正在准备页面...");

    [ObservableProperty]
    private string _currentPageTitle = string.Empty;

    [ObservableProperty]
    private bool _isAccountMenuOpen;

    [ObservableProperty]
    private string _currentPageKey = string.Empty;

    [RelayCommand]
    private void Navigate(string key)
    {
        if (!_navigationLookup.TryGetValue(key, out var navigationItem))
        {
            return;
        }

        if (navigationItem.RequiresPermission && !navigationItem.Permissions.Any(_session.HasPermission))
        {
            CurrentPage = new PlaceholderPageViewModel("无权限访问", $"当前页面需要权限之一：{navigationItem.PermissionHint}");
            CurrentPageTitle = "访问受限";
            return;
        }

        CurrentPage = _pageFactory[key]();
        CurrentPageTitle = navigationItem.Title;
        CurrentPageKey = key;
        IsAccountMenuOpen = false;
        UpdateSelectedNavigation(key);
    }

    [RelayCommand]
    private void ToggleSection(NavigationSectionViewModel? section)
    {
        if (section == null)
        {
            return;
        }

        section.IsExpanded = !section.IsExpanded;
    }

    [RelayCommand]
    private void ToggleAccountMenu()
    {
        if (IsAnonymous)
        {
            IsAccountMenuOpen = false;
            return;
        }

        IsAccountMenuOpen = !IsAccountMenuOpen;
    }

    [RelayCommand]
    private void CloseAccountMenu()
    {
        IsAccountMenuOpen = false;
    }

    [RelayCommand]
    private void OpenProfileCenter()
    {
        IsAccountMenuOpen = false;
        CurrentPage = new ProfileCenterPageViewModel(_apiClient, _session);
        CurrentPageTitle = "个人中心";
        CurrentPageKey = "profile-center";
        UpdateSelectedNavigation(string.Empty);
    }

    [RelayCommand]
    private void OpenSecuritySettings()
    {
        IsAccountMenuOpen = false;
        CurrentPage = new SecuritySettingsPageViewModel(_apiClient, _session);
        CurrentPageTitle = "安全设置";
        CurrentPageKey = "security-settings";
        UpdateSelectedNavigation(string.Empty);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        OnPropertyChanged(nameof(ThemeToggleIconData));
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        IsAccountMenuOpen = false;
        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            await _apiClient.LogoutAsync(_session.AccessToken);
        }

        _session.SetPendingLoginMessage("已安全退出登录。", isSuccess: true);
        _session.Clear();
    }

    private void UpdateSelectedNavigation(string key)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void RefreshNavigationVisibility()
    {
        foreach (var item in NavigationItems)
        {
            item.IsVisible = item.Key switch
            {
                "login" or "register" => false,
                _ => IsAuthenticated
            };
        }
    }

    private LoginPageViewModel CreateLoginPage(string? initialUserName = null, string? initialStatusMessage = null, bool initialSuccessMessage = false)
    {
        return new LoginPageViewModel(
            _apiClient,
            _session,
            () => Navigate("dashboard"),
            () => Navigate("register"),
            initialUserName,
            initialStatusMessage,
            initialSuccessMessage);
    }

    private RegisterPageViewModel CreateRegisterPage()
    {
        return new RegisterPageViewModel(
            _apiClient,
            (userName, statusMessage, isSuccess) =>
            {
                CurrentPage = CreateLoginPage(userName, statusMessage, isSuccess);
                CurrentPageTitle = "登录";
                CurrentPageKey = "login";
                IsAccountMenuOpen = false;
                UpdateSelectedNavigation("login");
            });
    }

    private void HandleAuthenticationStateChanged()
    {
        if (IsAuthenticated || IsLoginPageSelected || IsRegisterPageSelected)
        {
            return;
        }

        if (_session.TryConsumePendingLoginMessage(out var message, out var isSuccess))
        {
            ShowLoginPage(message, isSuccess);
            return;
        }

        ShowLoginPage();
    }

    private void ShowLoginPage(string? initialStatusMessage = null, bool initialSuccessMessage = false)
    {
        CurrentPage = CreateLoginPage(initialStatusMessage: initialStatusMessage, initialSuccessMessage: initialSuccessMessage);
        CurrentPageTitle = "登录";
        CurrentPageKey = "login";
        IsAccountMenuOpen = false;
        UpdateSelectedNavigation("login");
    }

    partial void OnCurrentPageKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsLoginPageSelected));
        OnPropertyChanged(nameof(IsRegisterPageSelected));
    }
}

public partial class PlaceholderPageViewModel : ViewModelBase
{
    public PlaceholderPageViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _description;
}
