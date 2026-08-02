using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumine.AuthPortal;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;
using System.Security.Cryptography;
using System.Text;

namespace Lumine.AuthPortal.ViewModels.Pages;

public partial class OidcPlaygroundPageViewModel : ViewModelBase
{
    private readonly PortalApiClient _apiClient;
    private readonly PortalSession _session;

    public OidcPlaygroundPageViewModel(PortalApiClient apiClient, PortalSession session)
    {
        _apiClient = apiClient;
        _session = session;
        RedirectUri = AppEnvironment.DefaultOidcRedirectUri;
        Scope = AppEnvironment.DefaultOidcScope;
        ClientId = AppEnvironment.DefaultOidcClientId;
        GeneratePkce();
        _ = LoadConfigurationAsync();
    }

    [ObservableProperty] private string _clientId;
    [ObservableProperty] private string _redirectUri;
    [ObservableProperty] private string _scope;
    [ObservableProperty] private string _state = string.Empty;
    [ObservableProperty] private string _nonce = string.Empty;
    [ObservableProperty] private string _codeVerifier = string.Empty;
    [ObservableProperty] private string _codeChallenge = string.Empty;
    [ObservableProperty] private string _authorizeCode = string.Empty;
    [ObservableProperty] private string _authorizeRedirectUrl = string.Empty;
    [ObservableProperty] private string _tokenAccessToken = string.Empty;
    [ObservableProperty] private string _tokenIdToken = string.Empty;
    [ObservableProperty] private bool _showSensitiveResults;
    [ObservableProperty] private string _userInfoSummary = "尚未调用 userinfo。";
    [ObservableProperty] private string _statusMessage = "可在此串联 authorize -> token -> userinfo。";
    [ObservableProperty] private string _runtimeSummary = "正在读取服务配置。";
    [ObservableProperty] private string _issuer = "尚未读取";
    [ObservableProperty] private string _authorizationEndpoint = "尚未读取";
    [ObservableProperty] private string _tokenEndpoint = "尚未读取";
    [ObservableProperty] private bool _isBusy;

    public string EndpointSummary => $"Issuer: {Issuer}\nAuthorize: {AuthorizationEndpoint}\nToken: {TokenEndpoint}";

    public string TokenAccessTokenDisplay => FormatSensitive(TokenAccessToken);

    public string TokenIdTokenDisplay => FormatSensitive(TokenIdToken);

    public string SensitiveResultsToggleText => ShowSensitiveResults ? "隐藏敏感结果" : "显示敏感结果";

    [RelayCommand]
    private void RegeneratePkce()
    {
        GeneratePkce();
        StatusMessage = "已生成新的 state / nonce / PKCE 参数。";
    }

    [RelayCommand]
    private async Task RequestAuthorizeAsync()
    {
        if (!_session.IsAuthenticated)
        {
            StatusMessage = "请先登录，再执行 authorize。";
            return;
        }

        if (!ValidateRequest())
        {
            return;
        }

        IsBusy = true;
        var result = await _apiClient.AuthorizeAsync(_session.AccessToken, new AuthorizeRequestDto(
            "code",
            ClientId,
            RedirectUri,
            Scope,
            State,
            Nonce,
            CodeChallenge,
            "S256",
            "approve",
            "json"));

        if (result.IsSuccess && result.Data != null)
        {
            AuthorizeCode = result.Data.Code;
            AuthorizeRedirectUrl = result.Data.RedirectUrl;
            StatusMessage = $"已拿到授权码，有效期 {result.Data.ExpiresIn} 秒。";
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "authorize 失败。";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ExchangeTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(AuthorizeCode))
        {
            StatusMessage = "请先获取授权码。";
            return;
        }

        if (!ValidateRequest())
        {
            return;
        }

        IsBusy = true;
        var result = await _apiClient.ExchangeCodeAsync(ClientId, AuthorizeCode, RedirectUri, CodeVerifier);
        if (result.IsSuccess && result.Data != null)
        {
            TokenAccessToken = result.Data.AccessToken;
            TokenIdToken = result.Data.IdToken ?? string.Empty;
            StatusMessage = "授权码换 token 成功，令牌默认已脱敏。";
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "token 兑换失败。";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadUserInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(TokenAccessToken))
        {
            StatusMessage = "请先完成 token 兑换。";
            return;
        }

        IsBusy = true;
        var result = await _apiClient.GetUserInfoAsync(TokenAccessToken);
        if (result.IsSuccess && result.Data != null)
        {
            UserInfoSummary = $"sub: {result.Data.Sub}\nusername: {result.Data.PreferredUsername}\nname: {result.Data.Name}\nemail: {result.Data.Email}\nroles: {string.Join(',', result.Data.Roles ?? Array.Empty<string>())}\npermissions: {string.Join(',', result.Data.Permissions ?? Array.Empty<string>())}";
            StatusMessage = "userinfo 调用成功。";
        }
        else
        {
            UserInfoSummary = result.ErrorMessage ?? "userinfo 调用失败。";
            StatusMessage = UserInfoSummary;
        }

        IsBusy = false;
    }

    private void GeneratePkce()
    {
        State = ToBase64Url(RandomNumberGenerator.GetBytes(24));
        Nonce = ToBase64Url(RandomNumberGenerator.GetBytes(24));
        CodeVerifier = ToBase64Url(RandomNumberGenerator.GetBytes(32));
        using var sha256 = SHA256.Create();
        CodeChallenge = ToBase64Url(sha256.ComputeHash(Encoding.ASCII.GetBytes(CodeVerifier)));
        AuthorizeCode = string.Empty;
        AuthorizeRedirectUrl = string.Empty;
        TokenAccessToken = string.Empty;
        TokenIdToken = string.Empty;
        ShowSensitiveResults = false;
        UserInfoSummary = "尚未调用 userinfo。";
    }

    [RelayCommand]
    private void ToggleSensitiveResults()
    {
        ShowSensitiveResults = !ShowSensitiveResults;
    }

    partial void OnTokenAccessTokenChanged(string value) => OnPropertyChanged(nameof(TokenAccessTokenDisplay));

    partial void OnTokenIdTokenChanged(string value) => OnPropertyChanged(nameof(TokenIdTokenDisplay));

    partial void OnShowSensitiveResultsChanged(bool value)
    {
        OnPropertyChanged(nameof(TokenAccessTokenDisplay));
        OnPropertyChanged(nameof(TokenIdTokenDisplay));
        OnPropertyChanged(nameof(SensitiveResultsToggleText));
    }

    private string FormatSensitive(string value)
    {
        if (ShowSensitiveResults || string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= 12 ? "••••••••" : $"{value[..6]}••••{value[^4..]}";
    }

    private async Task LoadConfigurationAsync()
    {
        var runtime = await _apiClient.GetRuntimeConfigurationAsync();
        if (runtime.IsSuccess && runtime.Data != null)
        {
            var data = runtime.Data;
            ClientId = string.IsNullOrWhiteSpace(data.DefaultClientId) ? AppEnvironment.DefaultOidcClientId : data.DefaultClientId;
            RedirectUri = string.IsNullOrWhiteSpace(data.DefaultRedirectUri) ? AppEnvironment.DefaultOidcRedirectUri : data.DefaultRedirectUri;
            RuntimeSummary = $"服务地址：{data.ApiBase ?? _apiClient.BaseUrl}\n客户端：{data.DefaultClientName ?? ClientId}\n回调地址：{RedirectUri}";
        }
        else
        {
            RuntimeSummary = runtime.ErrorMessage ?? "读取服务配置失败，已使用本地默认值。";
        }

        var discovery = await _apiClient.GetDiscoveryAsync();
        if (discovery.IsSuccess && discovery.Data != null)
        {
            Issuer = discovery.Data.Issuer ?? "未返回 Issuer";
            AuthorizationEndpoint = discovery.Data.AuthorizationEndpoint ?? "未返回 Authorize 地址";
            TokenEndpoint = discovery.Data.TokenEndpoint ?? "未返回 Token 地址";
            OnPropertyChanged(nameof(EndpointSummary));
            StatusMessage = "服务配置已就绪，可开始授权码联调。";
        }
        else
        {
            RuntimeSummary = $"{RuntimeSummary}\nDiscovery：{discovery.ErrorMessage ?? "读取失败"}";
            StatusMessage = "服务配置已读取，但 Discovery 读取失败。";
        }
    }

    private bool ValidateRequest()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            StatusMessage = "client_id 不能为空。";
            return false;
        }

        if (!Uri.TryCreate(RedirectUri, UriKind.Absolute, out var redirectUri)
            || (redirectUri.Scheme != Uri.UriSchemeHttp && redirectUri.Scheme != Uri.UriSchemeHttps))
        {
            StatusMessage = "redirect_uri 必须是完整的 http 或 https 地址，并且需要在客户端白名单中。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Scope))
        {
            StatusMessage = "至少请求一个 scope。";
            return false;
        }

        return true;
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
