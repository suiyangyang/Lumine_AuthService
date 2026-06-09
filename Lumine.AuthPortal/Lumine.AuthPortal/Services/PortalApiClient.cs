using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Lumine.AuthPortal.Models;

namespace Lumine.AuthPortal.Services;

public sealed class PortalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public PortalApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string BaseUrl
    {
        get => _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? AppEnvironment.DefaultServerBaseUrl;
        set => _httpClient.BaseAddress = new Uri(EnsureTrailingSlash(value));
    }

    public async Task<ApiResult<OpenIdConfigurationDto>> GetDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<OpenIdConfigurationDto>(HttpMethod.Get, "/.well-known/openid-configuration", cancellationToken: cancellationToken);
        return result;
    }

    public Task<ApiResult<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<LoginResponseDto>(HttpMethod.Post, "/api/auth/login", request, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<RegisterResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<RegisterResponseDto>(HttpMethod.Post, "/api/auth/register", request, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<UserInfoDto>> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserInfoDto>(HttpMethod.Get, "/connect/userinfo", bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<AuthorizeResponseDto>> AuthorizeAsync(string accessToken, AuthorizeRequestDto request, CancellationToken cancellationToken = default)
    {
        var path = BuildQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = request.ResponseType,
            ["client_id"] = request.ClientId,
            ["redirect_uri"] = request.RedirectUri,
            ["scope"] = request.Scope,
            ["state"] = request.State,
            ["nonce"] = request.Nonce,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = request.CodeChallengeMethod,
            ["consent"] = request.Consent,
            ["response_mode"] = request.ResponseMode
        });

        return SendAsync<AuthorizeResponseDto>(HttpMethod.Get, path, bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public async Task<ApiResult<TokenResponseDto>> ExchangeCodeAsync(string clientId, string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            })
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions, cancellationToken);
                return ApiResult<TokenResponseDto>.Success(new TokenResponseDto(
                    payload.GetProperty("access_token").GetString() ?? string.Empty,
                    payload.TryGetProperty("refresh_token", out var refreshToken) ? refreshToken.GetString() : null,
                    payload.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null,
                    payload.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
                    payload.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 0,
                    payload.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString() ?? "Bearer" : "Bearer"));
            }

            var message = await TryReadErrorMessageAsync(response, cancellationToken);
            return ApiResult<TokenResponseDto>.Failure(message, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return ApiResult<TokenResponseDto>.Failure($"请求失败：{ex.Message}");
        }
    }

    public Task<ApiResult<PagedResultDto<UserDto>>> GetUsersAsync(string accessToken, int pageIndex = 1, int pageSize = 10, string? keyword = null, CancellationToken cancellationToken = default)
    {
        var path = BuildQueryString("/api/users", new Dictionary<string, string?>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["keyword"] = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim()
        });

        return SendAsync<PagedResultDto<UserDto>>(HttpMethod.Get, path, bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<UserDto>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserDto>(HttpMethod.Get, "/api/users/me", bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<UserDto>> UpdateCurrentUserAsync(string accessToken, UpdateCurrentUserProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserDto>(HttpMethod.Put, "/api/users/me", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<object>> ChangeCurrentUserPasswordAsync(string accessToken, ChangeCurrentUserPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(HttpMethod.Post, "/api/users/me/change-password", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<PagedResultDto<RoleDto>>> GetRolesAsync(string accessToken, int pageIndex = 1, int pageSize = 10, string? keyword = null, CancellationToken cancellationToken = default)
    {
        var path = BuildQueryString("/api/roles", new Dictionary<string, string?>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["keyword"] = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim()
        });

        return SendAsync<PagedResultDto<RoleDto>>(HttpMethod.Get, path, bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<PagedResultDto<PermissionDto>>> GetPermissionsAsync(string accessToken, int pageIndex = 1, int pageSize = 10, string? keyword = null, CancellationToken cancellationToken = default)
    {
        var path = BuildQueryString("/api/permissions", new Dictionary<string, string?>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["keyword"] = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim()
        });

        return SendAsync<PagedResultDto<PermissionDto>>(HttpMethod.Get, path, bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<PagedResultDto<OidcClientDto>>> GetClientsAsync(string accessToken, int pageIndex = 1, int pageSize = 10, string? keyword = null, CancellationToken cancellationToken = default)
    {
        var path = BuildQueryString("/api/clients", new Dictionary<string, string?>
        {
            ["pageIndex"] = pageIndex.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["keyword"] = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim()
        });

        return SendAsync<PagedResultDto<OidcClientDto>>(HttpMethod.Get, path, bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<DashboardSummaryDto>> GetDashboardSummaryAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        return SendAsync<DashboardSummaryDto>(HttpMethod.Get, "/api/dashboard/summary", bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<UserDto>> CreateUserAsync(string accessToken, CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserDto>(HttpMethod.Post, "/api/users", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<UserDto>> UpdateUserAsync(string accessToken, Guid id, UpdateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserDto>(HttpMethod.Put, $"/api/users/{id}", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<UserDto>> UpdateUserStatusAsync(string accessToken, Guid id, UpdateUserStatusRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserDto>(HttpMethod.Put, $"/api/users/{id}/status", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<UserDto>> ResetUserPasswordAsync(string accessToken, Guid id, ResetUserPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserDto>(HttpMethod.Post, $"/api/users/{id}/reset-password", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<object>> DeleteUserAsync(string accessToken, Guid id, CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(HttpMethod.Delete, $"/api/users/{id}", bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<UserDto>> ReplaceUserRolesAsync(string accessToken, Guid id, ReplaceUserRolesRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<UserDto>(HttpMethod.Put, $"/api/users/{id}/roles", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<RoleDto>> CreateRoleAsync(string accessToken, CreateRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<RoleDto>(HttpMethod.Post, "/api/roles", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<RoleDto>> UpdateRoleAsync(string accessToken, Guid id, UpdateRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<RoleDto>(HttpMethod.Put, $"/api/roles/{id}", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<object>> DeleteRoleAsync(string accessToken, Guid id, CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(HttpMethod.Delete, $"/api/roles/{id}", bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<RoleDto>> ReplaceRolePermissionsAsync(string accessToken, Guid id, ReplaceRolePermissionsRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<RoleDto>(HttpMethod.Put, $"/api/roles/{id}/permissions", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<PermissionDto>> CreatePermissionAsync(string accessToken, CreatePermissionRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<PermissionDto>(HttpMethod.Post, "/api/permissions", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<PermissionDto>> UpdatePermissionAsync(string accessToken, Guid id, UpdatePermissionRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<PermissionDto>(HttpMethod.Put, $"/api/permissions/{id}", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<object>> DeletePermissionAsync(string accessToken, Guid id, CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(HttpMethod.Delete, $"/api/permissions/{id}", bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    public Task<ApiResult<OidcClientDto>> CreateClientAsync(string accessToken, SaveClientRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<OidcClientDto>(HttpMethod.Post, "/api/clients", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<OidcClientDto>> UpdateClientAsync(string accessToken, Guid id, SaveClientRequestDto request, CancellationToken cancellationToken = default)
    {
        return SendAsync<OidcClientDto>(HttpMethod.Put, $"/api/clients/{id}", request, accessToken, cancellationToken);
    }

    public Task<ApiResult<object>> DeleteClientAsync(string accessToken, Guid id, CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(HttpMethod.Delete, $"/api/clients/{id}", bearerToken: accessToken, cancellationToken: cancellationToken);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body, options: _jsonOptions);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
                return ApiResult<T>.Success(data);
            }

            var message = await TryReadErrorMessageAsync(response, cancellationToken);
            return ApiResult<T>.Failure(message, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return ApiResult<T>.Failure($"请求失败：{ex.Message}");
        }
    }

    private async Task<string> TryReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var document = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions, cancellationToken);
            if (document.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? $"请求失败，状态码 {(int)response.StatusCode}";
            }

            if (document.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? $"请求失败，状态码 {(int)response.StatusCode}";
            }
        }
        catch
        {
        }

        return $"请求失败，状态码 {(int)response.StatusCode}";
    }

    private static string EnsureTrailingSlash(string value)
    {
        return AppEnvironment.EnsureTrailingSlash(value);
    }

    private static string BuildQueryString(string path, IDictionary<string, string?> parameters)
    {
        var query = string.Join("&", parameters
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}"));

        return string.IsNullOrWhiteSpace(query) ? path : $"{path}?{query}";
    }
}
