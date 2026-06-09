using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        RedirectUri = "http://localhost:5235/signin-oidc";
        Scope = "openid profile email roles permissions";
        ClientId = "lumine-demo-client";
        GeneratePkce();
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
    [ObservableProperty] private string _userInfoSummary = "尚未调用 userinfo。";
    [ObservableProperty] private string _statusMessage = "可在此串联 authorize -> token -> userinfo。";
    [ObservableProperty] private bool _isBusy;

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

        IsBusy = true;
        var result = await _apiClient.ExchangeCodeAsync(ClientId, AuthorizeCode, RedirectUri, CodeVerifier);
        if (result.IsSuccess && result.Data != null)
        {
            TokenAccessToken = result.Data.AccessToken;
            TokenIdToken = result.Data.IdToken ?? string.Empty;
            StatusMessage = "授权码换 token 成功。";
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
        UserInfoSummary = "尚未调用 userinfo。";
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
