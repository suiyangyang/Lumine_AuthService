using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;

namespace Lumine.AuthPortal.ViewModels.Pages;

public abstract partial class AccountPageViewModelBase : ViewModelBase
{
    protected AccountPageViewModelBase(PortalApiClient apiClient, PortalSession session)
    {
        ApiClient = apiClient;
        Session = session;
    }

    protected PortalApiClient ApiClient { get; }

    protected PortalSession Session { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "准备加载账户信息。";

    protected bool EnsureAuthenticated()
    {
        if (Session.IsAuthenticated)
        {
            return true;
        }

        StatusMessage = "请先登录后再访问账户页面。";
        return false;
    }
}

public partial class ProfileCenterPageViewModel : AccountPageViewModelBase
{
    public ProfileCenterPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        Session.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(RoleTags));
            OnPropertyChanged(nameof(PrimaryRole));
            OnPropertyChanged(nameof(PermissionSummary));
            OnPropertyChanged(nameof(SummaryCards));
        };

        _ = LoadAsync();
    }

    [ObservableProperty]
    private UserDto? _profile;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _nickName = string.Empty;

    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Profile?.NickName) ? (Profile?.UserName ?? Session.UserName) : Profile.NickName!;

    public string LoginStatus => Session.IsAuthenticated ? "当前已登录，可编辑个人资料并同步到后台。" : "当前未登录，仅展示默认信息。";

    public string PrimaryRole => RoleTags.FirstOrDefault() ?? (Session.IsAuthenticated ? "未分配角色" : "访客");

    public IReadOnlyList<string> RoleTags => Profile?.Roles.Select(role => role.Name).ToArray()
        ?? (Session.Roles.Count == 0
            ? (Session.IsAuthenticated ? ["未分配"] : ["访客"])
            : Session.Roles);

    public string PermissionSummary => Session.Permissions.Count switch
    {
        0 => Session.IsAuthenticated ? "当前未分配细粒度权限。" : "登录后可查看权限摘要。",
        <= 6 => $"已分配权限：{string.Join("、", Session.Permissions)}",
        _ => $"已分配 {Session.Permissions.Count} 项权限，包含：{string.Join("、", Session.Permissions.Take(6))} 等。"
    };

    public IReadOnlyList<ProfileInfoCardItemViewModel> SummaryCards =>
    [
        new("账号状态", Session.IsAuthenticated ? "已登录" : "未登录", Session.IsAuthenticated ? "当前会话有效，可直接访问后台页面。" : "登录后可使用后台功能。", "#2563EB"),
        new("主角色", PrimaryRole, "用于快速识别当前账号所属职责。", "#7C3AED"),
        new("角色数量", RoleTags.Count.ToString(), "展示当前账号拥有的角色标签数。", "#059669"),
        new("权限数量", Session.Permissions.Count.ToString(), "权限数据来自当前登录态。", "#D97706")
    ];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        IsBusy = true;
        var result = await ApiClient.GetCurrentUserAsync(Session.AccessToken);
        if (result.IsSuccess && result.Data != null)
        {
            ApplyProfile(result.Data, syncSession: false);
            StatusMessage = "已加载个人资料。";
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "加载个人资料失败。";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Email))
        {
            StatusMessage = "用户名和邮箱不能为空。";
            return;
        }

        IsBusy = true;
        var result = await ApiClient.UpdateCurrentUserAsync(Session.AccessToken, new UpdateCurrentUserProfileRequestDto(
            UserName.Trim(),
            Email.Trim(),
            EmptyToNull(NickName),
            EmptyToNull(PhoneNumber)));

        if (result.IsSuccess && result.Data != null)
        {
            ApplyProfile(result.Data, syncSession: true);
            StatusMessage = "个人资料已保存。";
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "保存个人资料失败。";
        }

        IsBusy = false;
    }

    private void ApplyProfile(UserDto profile, bool syncSession)
    {
        Profile = profile;
        UserName = profile.UserName;
        Email = profile.Email;
        NickName = profile.NickName ?? string.Empty;
        PhoneNumber = profile.PhoneNumber ?? string.Empty;

        if (syncSession)
        {
            Session.UserName = profile.UserName;
            Session.Email = profile.Email;
            Session.Roles = profile.Roles.Select(role => role.Name).ToArray();
        }

        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(RoleTags));
        OnPropertyChanged(nameof(PrimaryRole));
        OnPropertyChanged(nameof(PermissionSummary));
        OnPropertyChanged(nameof(SummaryCards));
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public partial class SecuritySettingsPageViewModel : AccountPageViewModelBase
{
    public SecuritySettingsPageViewModel(PortalApiClient apiClient, PortalSession session) : base(apiClient, session)
    {
        Session.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SecurityStatus));
            OnPropertyChanged(nameof(SecurityCards));
            OnPropertyChanged(nameof(DeviceActivities));
        };
    }

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmNewPassword = string.Empty;

    public string SecurityStatus => Session.IsAuthenticated
        ? "已接入当前账号改密能力，MFA 与设备管理可继续扩展后端接口。"
        : "当前未登录，安全设置仅显示默认建议。";

    public IReadOnlyList<SecurityCardItemViewModel> SecurityCards =>
    [
        new("登录状态", Session.IsAuthenticated ? "会话有效" : "未登录", Session.IsAuthenticated ? "可立即执行当前账号改密。" : "登录后可查看并修改账号安全设置。"),
        new("密码策略", "建议启用强密码", "已支持当前账号修改密码，后续可接入到期提醒与历史密码限制。"),
        new("多因子认证", "待接入", "建议后续接入短信、邮件或 TOTP 二次验证。"),
        new("设备管理", "待接入", "当前展示本地会话摘要，后续可接入设备列表与强制下线。")
    ];

    public IReadOnlyList<string> SecurityTips =>
    [
        "为高权限账号启用多因子认证，降低口令泄露风险。",
        "定期轮换密码，并限制弱口令与历史密码重复使用。",
        "为异常设备登录增加告警通知与强制下线机制。"
    ];

    public IReadOnlyList<SecurityActivityItemViewModel> DeviceActivities => Session.IsAuthenticated
        ?
        [
            new("当前浏览器会话", "活跃中", "刚刚", "本地 Web Portal 当前实例。"),
            new("账号邮箱", "已同步", "最近刷新", string.IsNullOrWhiteSpace(Session.Email) ? "邮箱未配置" : Session.Email),
            new("MFA 状态", "未启用", "待配置", "后续接入 MFA 接口后可在此显示真实状态。")
        ]
        :
        [
            new("匿名访问", "访客模式", "当前", "登录后展示更多设备和安全记录。")
        ];

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "请输入当前密码和新密码。";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
        {
            StatusMessage = "两次输入的新密码不一致。";
            return;
        }

        IsBusy = true;
        var result = await ApiClient.ChangeCurrentUserPasswordAsync(Session.AccessToken, new ChangeCurrentUserPasswordRequestDto(CurrentPassword, NewPassword));
        if (result.IsSuccess)
        {
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
            StatusMessage = "密码修改成功，请妥善保管新密码。";
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "修改密码失败。";
        }

        IsBusy = false;
    }
}

public sealed class ProfileInfoCardItemViewModel
{
    public ProfileInfoCardItemViewModel(string title, string value, string description, string accentColor)
    {
        Title = title;
        Value = value;
        Description = description;
        AccentColor = accentColor;
    }

    public string Title { get; }

    public string Value { get; }

    public string Description { get; }

    public string AccentColor { get; }
}

public sealed class SecurityCardItemViewModel
{
    public SecurityCardItemViewModel(string title, string value, string description)
    {
        Title = title;
        Value = value;
        Description = description;
    }

    public string Title { get; }

    public string Value { get; }

    public string Description { get; }
}

public sealed class SecurityActivityItemViewModel
{
    public SecurityActivityItemViewModel(string title, string status, string timeText, string description)
    {
        Title = title;
        Status = status;
        TimeText = timeText;
        Description = description;
    }

    public string Title { get; }

    public string Status { get; }

    public string TimeText { get; }

    public string Description { get; }
}