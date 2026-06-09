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
    private string _userName = "admin";

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

    public string OidcToggleText => ShowOidcParameters ? "隐藏 OIDC 调试参数" : "显示 OIDC 调试参数";

    public char PasswordMaskChar => ShowPassword ? '\0' : '●';

    public StreamGeometry PasswordVisibilityIconData => NavigationIconData.Get(ShowPassword ? "close" : "eye");

    public string PasswordVisibilityToolTip => ShowPassword ? "隐藏密码" : "显示密码";

    public LoginPageViewModel(PortalApiClient apiClient, PortalSession session, Action onLoginSucceeded, Action openRegister)
    {
        _apiClient = apiClient;
        _session = session;
        _onLoginSucceeded = onLoginSucceeded;
        _openRegister = openRegister;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsBusy = true;
        StatusMessage = "正在调用登录接口...";

        var scope = ShowOidcParameters ? Scope : null;
        var clientId = ShowOidcParameters ? ClientId : null;
        var nonce = ShowOidcParameters ? Nonce : null;

        var result = await _apiClient.LoginAsync(new LoginRequestDto(UserName, Password, scope, clientId, nonce));
        if (result.IsSuccess && result.Data != null)
        {
            _session.ApplyLogin(result.Data);
            StatusMessage = $"登录成功，欢迎 {result.Data.User?.UserName ?? UserName}。";
            _onLoginSucceeded();
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "登录失败。";
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
        StatusMessage = value
            ? "已开启 OIDC 调试参数，可用于第三方授权联调。"
            : "请输入账号密码后登录。";
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
}

public partial class RegisterPageViewModel : ViewModelBase
{
    private readonly PortalApiClient _apiClient;
    private readonly Action _openLogin;

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

    public RegisterPageViewModel(PortalApiClient apiClient, Action openLogin)
    {
        _apiClient = apiClient;
        _openLogin = openLogin;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsBusy = true;
        StatusMessage = "正在调用注册接口...";

        var result = await _apiClient.RegisterAsync(new RegisterRequestDto(UserName, Email, Password, NickName, PhoneNumber));
        StatusMessage = result.IsSuccess && result.Data != null
            ? $"注册成功：{result.Data.UserName}"
            : result.ErrorMessage ?? "注册失败。";

        IsBusy = false;
    }

    [RelayCommand]
    private void OpenLogin()
    {
        _openLogin();
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
