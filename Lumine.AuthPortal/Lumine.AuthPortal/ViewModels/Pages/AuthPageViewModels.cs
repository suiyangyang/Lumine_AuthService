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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorMessage;

    [ObservableProperty]
    private bool _isSuccessMessage;

    public string OidcToggleText => ShowOidcParameters ? "隐藏 OIDC 调试参数" : "显示 OIDC 调试参数";

    public string LoginButtonText => IsBusy ? "登录中..." : "登录";

    public bool CanInteract => !IsBusy;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

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
        if (value)
        {
            SetStatus("已开启 OIDC 调试参数，可用于第三方授权联调。");
        }
        else if (!IsBusy && !IsErrorMessage && !IsSuccessMessage)
        {
            SetStatus(string.Empty);
        }
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

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isErrorMessage;

    [ObservableProperty]
    private bool _isSuccessMessage;

    public string RegisterButtonText => IsBusy ? "注册中..." : "注册";

    public bool CanInteract => !IsBusy;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

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

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
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
    private readonly PortalApiClient _apiClient;
    private readonly PortalSession _session;
    private const string DefaultScope = "openid profile email roles permissions";
    private const string DefaultClientId = "lumine-demo-client";
    private const string DefaultRedirectUri = "http://localhost:5173/signin-oidc";

    public ConsentPageViewModel(PortalApiClient apiClient, PortalSession session)
    {
        _apiClient = apiClient;
        _session = session;
        _ = LoadPreviewAsync();
    }

    [ObservableProperty]
    private string _clientId = DefaultClientId;

    [ObservableProperty]
    private string _clientName = "Lumine Demo Client";

    [ObservableProperty]
    private string _redirectUri = DefaultRedirectUri;

    [ObservableProperty]
    private string _requestedScopes = "openid profile email roles permissions";

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private string _nonce = string.Empty;

    [ObservableProperty]
    private string _codeVerifier = string.Empty;

    [ObservableProperty]
    private string _codeChallenge = string.Empty;

    [ObservableProperty]
    private string _decisionMessage = "正在检查当前授权请求。";

    [ObservableProperty]
    private bool _isBusy;

    public bool CanSubmitDecision => _session.IsAuthenticated && !IsBusy;

    public bool HasDecisionMessage => !string.IsNullOrWhiteSpace(DecisionMessage);

    public string CurrentRequestSummary => $"{ClientName} · {ClientId}";

    public string PkceSummary => $"state: {State}{Environment.NewLine}nonce: {Nonce}{Environment.NewLine}challenge: {CodeChallenge}";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadPreviewAsync();
    }

    [RelayCommand]
    private async Task ApproveAsync()
    {
        if (!_session.IsAuthenticated)
        {
            DecisionMessage = "请先登录，再执行授权确认。";
            return;
        }

        IsBusy = true;
        var result = await _apiClient.AuthorizeAsync(
            _session.AccessToken,
            new AuthorizeRequestDto("code", ClientId, RedirectUri, RequestedScopes, State, Nonce, CodeChallenge, "S256", "approve", "json"));

        DecisionMessage = result.IsSuccess && result.Data != null
            ? $"授权已批准，已签发授权码：{result.Data.Code}"
            : result.ErrorMessage ?? "批准授权失败。";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task DenyAsync()
    {
        if (!_session.IsAuthenticated)
        {
            DecisionMessage = "请先登录，再执行授权确认。";
            return;
        }

        IsBusy = true;
        var result = await _apiClient.AuthorizeAsync(
            _session.AccessToken,
            new AuthorizeRequestDto("code", ClientId, RedirectUri, RequestedScopes, State, Nonce, CodeChallenge, "S256", "deny", "json"));

        DecisionMessage = result.IsSuccess
            ? "授权已拒绝。"
            : result.ErrorMessage ?? "拒绝授权失败。";
        IsBusy = false;
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmitDecision));
    }

    partial void OnDecisionMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasDecisionMessage));
    }

    partial void OnClientIdChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentRequestSummary));
    }

    partial void OnClientNameChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentRequestSummary));
    }

    partial void OnStateChanged(string value)
    {
        OnPropertyChanged(nameof(PkceSummary));
    }

    partial void OnNonceChanged(string value)
    {
        OnPropertyChanged(nameof(PkceSummary));
    }

    partial void OnCodeChallengeChanged(string value)
    {
        OnPropertyChanged(nameof(PkceSummary));
    }

    private async Task LoadPreviewAsync()
    {
        GeneratePkce();

        if (!_session.IsAuthenticated)
        {
            DecisionMessage = "请先登录，再查看授权确认。";
            return;
        }

        IsBusy = true;
        var result = await _apiClient.GetAuthorizePreviewAsync(
            _session.AccessToken,
            new AuthorizeRequestDto("code", ClientId, RedirectUri, DefaultScope, State, Nonce, CodeChallenge, "S256", string.Empty, "json"));

        if (result.IsSuccess && result.Data != null)
        {
            ClientName = result.Data.Client.ClientName;
            ClientId = result.Data.Client.ClientId;
            RequestedScopes = string.Join(' ', result.Data.Scopes);
            State = result.Data.State ?? State;
            DecisionMessage = result.Data.Message;
        }
        else
        {
            RequestedScopes = DefaultScope;
            DecisionMessage = result.ErrorMessage ?? "读取授权预览失败。";
        }

        IsBusy = false;
    }

    private void GeneratePkce()
    {
        State = ToBase64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        Nonce = ToBase64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        CodeVerifier = ToBase64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        CodeChallenge = ToBase64Url(sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(CodeVerifier)));
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
