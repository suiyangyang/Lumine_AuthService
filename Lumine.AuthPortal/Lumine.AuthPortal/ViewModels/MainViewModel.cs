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
    private readonly Dictionary<string, Func<ViewModelBase>> _pageFactory;
    private readonly Dictionary<string, NavigationItemViewModel> _navigationLookup;

    public MainViewModel(PortalApiClient apiClient, PortalSession session)
    {
        _apiClient = apiClient;
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(PortalSession.AccessToken) or nameof(PortalSession.Permissions))
            {
                RefreshNavigationVisibility();
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsAnonymous));
                OnPropertyChanged(nameof(VisibleNavigationSections));
                OnPropertyChanged(nameof(HasAdminAccess));
            }

            if (args.PropertyName is nameof(PortalSession.AccessToken)
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

        var dashboard = new NavigationItemViewModel("dashboard", "仪表盘", "dashboard");
        var login = new NavigationItemViewModel("login", "登录", "login");
        var register = new NavigationItemViewModel("register", "注册", "register");
        var users = new NavigationItemViewModel("users", "用户管理", "users", "users.view", "users.manage");
        var roles = new NavigationItemViewModel("roles", "角色管理", "roles", "roles.view", "roles.manage");
        var permissions = new NavigationItemViewModel("permissions", "权限管理", "permissions", "permissions.view", "permissions.manage");
        var userGroups = new NavigationItemViewModel("user-groups", "用户组管理", "user-groups");
        var clients = new NavigationItemViewModel("clients", "OAuth 客户端", "clients", "clients.view", "clients.manage");
        var consent = new NavigationItemViewModel("consent", "授权确认", "consent");
        var tokens = new NavigationItemViewModel("tokens", "Token 管理", "tokens");
        var oidc = new NavigationItemViewModel("oidc", "OIDC Discovery", "oidc");
        var menus = new NavigationItemViewModel("menus", "菜单管理", "menus");
        var settings = new NavigationItemViewModel("settings", "系统配置", "settings");
        var audit = new NavigationItemViewModel("audit", "审计日志", "audit");

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
            ["login"] = () => new LoginPageViewModel(_apiClient, _session, () => Navigate("dashboard")),
            ["register"] = () => new RegisterPageViewModel(_apiClient),
            ["user-groups"] = () => new PlaceholderPageViewModel("用户组管理", "预留后台入口，后续可接入用户组列表、成员维护与角色映射。"),
            ["consent"] = () => new ConsentPageViewModel(),
            ["users"] = () => new UsersManagementPageViewModel(_apiClient, _session),
            ["roles"] = () => new RolesManagementPageViewModel(_apiClient, _session),
            ["permissions"] = () => new PermissionsManagementPageViewModel(_apiClient, _session),
            ["clients"] = () => new ClientsManagementPageViewModel(_apiClient, _session),
            ["tokens"] = () => new PlaceholderPageViewModel("Token 管理", "预留后台入口，后续可接入访问令牌、刷新令牌与吊销记录管理。"),
            ["oidc"] = () => new OidcPlaygroundPageViewModel(_apiClient, _session),
            ["menus"] = () => new PlaceholderPageViewModel("菜单管理", "预留后台入口，后续可接入菜单树、路由元数据与权限绑定配置。"),
            ["settings"] = () => new SystemSettingsPageViewModel(),
            ["audit"] = () => new AuditLogsPageViewModel()
        };

        RefreshNavigationVisibility();

        CurrentPage = _pageFactory["dashboard"]();
        CurrentPageTitle = "仪表盘";
        UpdateSelectedNavigation("dashboard");
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public IReadOnlyList<NavigationSectionViewModel> NavigationSections { get; }

    public IEnumerable<NavigationSectionViewModel> VisibleNavigationSections => NavigationSections
        .Where(section => section.Items.Any(item => item.IsVisible));

    public bool IsAuthenticated => _session.IsAuthenticated;

    public bool IsAnonymous => !IsAuthenticated;

    public PortalSession Session => _session;

    public StreamGeometry AccountMenuChevronIconData => NavigationIconData.Get("chevron-down");

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
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _currentPageTitle;

    [ObservableProperty]
    private bool _isAccountMenuOpen;

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
        UpdateSelectedNavigation(string.Empty);
    }

    [RelayCommand]
    private void OpenSecuritySettings()
    {
        IsAccountMenuOpen = false;
        CurrentPage = new SecuritySettingsPageViewModel(_apiClient, _session);
        CurrentPageTitle = "安全设置";
        UpdateSelectedNavigation(string.Empty);
    }

    [RelayCommand]
    private void Logout()
    {
        IsAccountMenuOpen = false;
        _session.Clear();
        Navigate("login");
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
                "login" or "register" => IsAnonymous,
                _ => IsAuthenticated || item.Key == "dashboard"
            };
        }
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
