using System;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;

namespace Lumine.AuthPortal.ViewModels.Pages;

public partial class LoginPageViewModel : ViewModelBase
{
    private readonly PortalApiClient _apiClient;
    private readonly PortalSession _session;
    private readonly Action _onLoginSucceeded;
    private readonly Action _openRegister;
    private const string DefaultScope = "openid profile email roles permissions";
    private const string DefaultClientId = "lumine-demo-client";
    private const string DefaultNonce = "portal-login-nonce";

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _showPassword;

    [ObservableProperty]
    private string _scope = DefaultScope;

    [ObservableProperty]
    private string _clientId = DefaultClientId;

    [ObservableProperty]
    private string _nonce = DefaultNonce;

    [ObservableProperty]
    private bool _showOidcParameters;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "请输入账号密码后登录。";

    [ObservableProperty]
    private bool _isErrorMessage;

    [ObservableProperty]
    private bool _isSuccessMessage;

    public string OidcToggleText => ShowOidcParameters ? "隐藏 OIDC 调试参数" : "显示 OIDC 调试参数";

    public string LoginButtonText => IsBusy ? "正在登录..." : "登录并进入后台";

    public bool CanInteract => !IsBusy;

    public char PasswordMaskChar => ShowPassword ? '\0' : '●';

    public StreamGeometry PasswordVisibilityIconData => NavigationIconData.Get(ShowPassword ? "close" : "eye");

    public string PasswordVisibilityToolTip => ShowPassword ? "隐藏密码" : "显示密码";

    public LoginPageViewModel(
        PortalApiClient apiClient,
        PortalSession session,
        Action onLoginSucceeded,
        Action openRegister,
        string? initialUserName = null,
        string? initialStatusMessage = null,
        bool initialSuccessMessage = false)
    {
        _apiClient = apiClient;
        _session = session;
        _onLoginSucceeded = onLoginSucceeded;
        _openRegister = openRegister;

        if (!string.IsNullOrWhiteSpace(initialUserName))
        {
            UserName = initialUserName;
        }

        if (!string.IsNullOrWhiteSpace(initialStatusMessage))
        {
            SetStatus(initialStatusMessage, isSuccess: initialSuccessMessage);
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            SetStatus("请输入用户名和密码。", isError: true);
            return;
        }

        IsBusy = true;
        SetStatus("正在调用登录接口...");

        var scope = ShowOidcParameters ? Scope : null;
        var clientId = ShowOidcParameters ? ClientId : null;
        var nonce = ShowOidcParameters ? Nonce : null;

        var result = await _apiClient.LoginAsync(new LoginRequestDto(UserName, Password, scope, clientId, nonce));
        if (result.IsSuccess && result.Data != null)
        {
            _session.ApplyLogin(result.Data);
            Password = string.Empty;
            SetStatus($"登录成功，欢迎 {result.Data.User?.UserName ?? UserName}。", isSuccess: true);
            _onLoginSucceeded();
        }
        else
        {
            SetStatus(result.ErrorMessage ?? "登录失败。", isError: true);
        }

        IsBusy = false;
    }

    [RelayCommand]
    private void ToggleOidcParameters()
    {
        ShowOidcParameters = !ShowOidcParameters;

        if (ShowOidcParameters)
        {
            if (string.IsNullOrWhiteSpace(Scope))
            {
                Scope = DefaultScope;
            }

            if (string.IsNullOrWhiteSpace(ClientId))
            {
                ClientId = DefaultClientId;
            }

            if (string.IsNullOrWhiteSpace(Nonce))
            {
                Nonce = DefaultNonce;
            }
        }
    }

    partial void OnShowOidcParametersChanged(bool value)
    {
        OnPropertyChanged(nameof(OidcToggleText));
        SetStatus(value
            ? "已开启 OIDC 调试参数，可用于第三方授权联调。"
            : "请输入账号密码后登录。");
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        ShowPassword = !ShowPassword;
    }

    [RelayCommand]
    private void OpenRegister()
    {
        _openRegister();
    }

    partial void OnShowPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordMaskChar));
        OnPropertyChanged(nameof(PasswordVisibilityIconData));
        OnPropertyChanged(nameof(PasswordVisibilityToolTip));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(LoginButtonText));
        OnPropertyChanged(nameof(CanInteract));
    }

    private void SetStatus(string message, bool isError = false, bool isSuccess = false)
    {
        StatusMessage = message;
        IsErrorMessage = isError;
        IsSuccessMessage = isSuccess;
    }
}

public partial class RegisterPageViewModel : ViewModelBase
{
    private readonly PortalApiClient _apiClient;
    private readonly Action<string?, string?, bool> _openLogin;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _nickName = string.Empty;

    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "填写信息后即可创建账号。";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isErrorMessage;

    [ObservableProperty]
    private bool _isSuccessMessage;

    public string RegisterButtonText => IsBusy ? "正在提交..." : "提交注册";

    public bool CanInteract => !IsBusy;

    public RegisterPageViewModel(PortalApiClient apiClient, Action<string?, string?, bool> openLogin)
    {
        _apiClient = apiClient;
        _openLogin = openLogin;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            SetStatus("用户名、邮箱和密码为必填项。", isError: true);
            return;
        }

        IsBusy = true;
        SetStatus("正在调用注册接口...");

        var result = await _apiClient.RegisterAsync(new RegisterRequestDto(UserName, Email, Password, NickName, PhoneNumber));
        if (result.IsSuccess && result.Data != null)
        {
            var registeredUserName = result.Data.UserName;
            UserName = string.Empty;
            Email = string.Empty;
            Password = string.Empty;
            NickName = string.Empty;
            PhoneNumber = string.Empty;
            SetStatus($"账号 {registeredUserName} 注册成功，请登录。", isSuccess: true);
            _openLogin(registeredUserName, $"账号 {registeredUserName} 注册成功，请登录。", true);
        }
        else
        {
            SetStatus(result.ErrorMessage ?? "注册失败。", isError: true);
        }

        IsBusy = false;
    }

    [RelayCommand]
    private void OpenLogin()
    {
        _openLogin(null, null, false);
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(RegisterButtonText));
        OnPropertyChanged(nameof(CanInteract));
    }

    private void SetStatus(string message, bool isError = false, bool isSuccess = false)
    {
        StatusMessage = message;
        IsErrorMessage = isError;
        IsSuccessMessage = isSuccess;
    }
}

public partial class ConsentPageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _clientName = "Lumine Demo Client";

    [ObservableProperty]
    private string _requestedScopes = "openid profile email roles permissions";

    [ObservableProperty]
    private string _state = "state-001";

    [ObservableProperty]
    private string _decisionMessage = "可作为授权确认页骨架，后续接入 `/connect/authorize` 的 `consent=approve|deny`。";

    [RelayCommand]
    private void Approve()
    {
        DecisionMessage = "已模拟同意授权，下一步可接入真实授权码回调流程。";
    }

    [RelayCommand]
    private void Deny()
    {
        DecisionMessage = "已模拟拒绝授权，后续可带 `error=access_denied` 回跳。";
    }
}
