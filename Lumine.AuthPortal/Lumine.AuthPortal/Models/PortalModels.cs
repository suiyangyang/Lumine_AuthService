using System.Text.Json.Serialization;

namespace Lumine.AuthPortal.Models;

public sealed record ApiResult<T>(bool IsSuccess, T? Data, string? ErrorMessage, int? StatusCode = null)
{
    public static ApiResult<T> Success(T? data) => new(true, data, null);

    public static ApiResult<T> Failure(string message, int? statusCode = null) => new(false, default, message, statusCode);
}

public sealed record OpenIdConfigurationDto(
    string? Issuer,
    string? AuthorizationEndpoint,
    string? TokenEndpoint,
    string? UserinfoEndpoint,
    IReadOnlyList<string>? ScopesSupported);

public sealed record PortalRuntimeConfigurationDto(
    string? ApiBase,
    string? Issuer,
    string? DefaultClientId,
    string? DefaultClientName,
    string? DefaultRedirectUri,
    string? Port);

public sealed record AuthorizeRequestDto(
    string ResponseType,
    string ClientId,
    string RedirectUri,
    string Scope,
    string State,
    string Nonce,
    string CodeChallenge,
    string CodeChallengeMethod,
    string Consent,
    string ResponseMode);

public sealed record AuthorizeResponseDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("redirect_uri")] string RedirectUri,
    [property: JsonPropertyName("redirect_url")] string RedirectUrl,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record LoginRequestDto(string UserName, string Password, string? Scope, string? ClientId, string? Nonce);

public sealed record RegisterRequestDto(string UserName, string Email, string Password, string? NickName, string? PhoneNumber);

public sealed record PermissionDto(Guid Id, string PermissionName, string DisplayName);

public sealed record RoleDto(Guid Id, string Name, IReadOnlyList<PermissionDto> Permissions);

public sealed record UserDto(
    Guid Id,
    string UserName,
    string Email,
    string? NickName,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<RoleDto> Roles)
{
    public string StatusText => IsActive ? "启用" : "禁用";

    public bool IsInactive => !IsActive;

    public string ToggleStatusActionText => IsActive ? "禁用" : "启用";

    public string RolesDisplay => Roles.Count == 0
        ? "未分配"
        : string.Join("、", Roles.Select(role => role.Name));

    public string CreatedAtDisplay => CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string LastLoginAtDisplay => LastLoginAtUtc.HasValue
        ? LastLoginAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "从未登录";
}

public sealed record UserProfileDto(
    Guid Id,
    string UserName,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record LoginResponseDto(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("user")] UserProfileDto? User);

public sealed record TokenResponseDto(string AccessToken, string? RefreshToken, string? IdToken, string? Scope, int ExpiresIn, string TokenType);

public sealed record RegisterResponseDto(Guid Id, string UserName, string Email, string? NickName, string? PhoneNumber, bool IsActive);

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize);

public sealed record OidcClientDto(Guid Id, string ClientId, string ClientName, string ClientType, IReadOnlyList<string> AllowedScopes, IReadOnlyList<string> RedirectUris, bool RequirePkce, bool IsActive, string? Description)
{
    public string StatusText => IsActive ? "启用" : "停用";

    public bool HasConfigurationWarning => IsActive
        && !string.Equals(ClientId, "labchrom_knowledge", StringComparison.OrdinalIgnoreCase)
        && (AllowedScopes.Count == 0 || RedirectUris.Count == 0);

    public string ConfigurationWarningText => !HasConfigurationWarning
        ? string.Empty
        : AllowedScopes.Count == 0 && RedirectUris.Count == 0
            ? "缺少 Scope 和 Redirect URI"
            : AllowedScopes.Count == 0
                ? "缺少允许 Scope"
                : "缺少 Redirect URI";

    public string AllowedScopesDisplay => AllowedScopes.Count == 0
        ? "未配置"
        : string.Join("、", AllowedScopes);
}

public sealed record CreateUserRequestDto(string UserName, string Email, string Password, string? NickName, string? PhoneNumber, bool IsActive, List<Guid>? RoleIds);

public sealed record UpdateUserRequestDto(string UserName, string Email, string? NickName, string? PhoneNumber, bool IsActive, string? Password);

public sealed record UpdateUserStatusRequestDto(bool IsActive);

public sealed record ResetUserPasswordRequestDto(string Password);

public sealed record UpdateCurrentUserProfileRequestDto(string UserName, string Email, string? NickName, string? PhoneNumber);

public sealed record ChangeCurrentUserPasswordRequestDto(string CurrentPassword, string NewPassword);

public sealed record ApiKeyDto(
    Guid Id,
    Guid UserId,
    string ClientId,
    string KeyPrefix,
    string KeySuffix,
    DateTime CreateTime,
    DateTime? ExpireTime,
    string Status,
    DateTime? LastUsedTime)
{
    public string StatusText => string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase) ? "有效" : "已删除";

    public string? PlainTextKey { get; init; }

    public string MaskedKeyDisplay
    {
        get
        {
            var prefix = KeyPrefix.StartsWith("sk-", StringComparison.OrdinalIgnoreCase) ? KeyPrefix[3..] : KeyPrefix;
            var firstThree = prefix.Length >= 3 ? prefix[..3] : prefix;
            var suffix = string.IsNullOrWhiteSpace(KeySuffix) ? "???" : KeySuffix[^Math.Min(3, KeySuffix.Length)..];
            return $"sk-{firstThree}***{suffix}";
        }
    }

    public bool CanCopy => !string.IsNullOrWhiteSpace(PlainTextKey);

    public string CreateTimeDisplay => CreateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string ExpireTimeDisplay => ExpireTime.HasValue ? ExpireTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "永久有效";

    public string LastUsedTimeDisplay => LastUsedTime.HasValue ? LastUsedTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "尚未使用";

    public bool IsRevoked => !string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);
}

public sealed record CreatedApiKeyDto(
    Guid Id,
    Guid UserId,
    string ClientId,
    string KeyPrefix,
    string KeySuffix,
    DateTime CreateTime,
    DateTime? ExpireTime,
    string Status,
    DateTime? LastUsedTime,
    string ApiKey);

public sealed record CreateApiKeyRequestDto(string ClientId, DateTime? ExpireTime = null);

public sealed record ReplaceUserRolesRequestDto(List<Guid> RoleIds);

public sealed record CreateRoleRequestDto(string Name, List<Guid>? PermissionIds);

public sealed record UpdateRoleRequestDto(string Name);

public sealed record ReplaceRolePermissionsRequestDto(List<Guid> PermissionIds);

public sealed record CreatePermissionRequestDto(string PermissionName, string? DisplayName);

public sealed record UpdatePermissionRequestDto(string PermissionName, string? DisplayName);

public sealed record SaveClientRequestDto(string ClientId, string ClientName, string ClientType, List<string> AllowedScopes, List<string> RedirectUris, bool RequirePkce, bool IsActive, string? Description);

public sealed record UserInfoDto(string? Sub, string? PreferredUsername, string? Name, string? Email, IReadOnlyList<string>? Roles, IReadOnlyList<string>? Permissions);

public sealed record DashboardMetricDto(string Title, int? Value, bool IsVisible, string? Description);

public sealed record DashboardTrendPointDto(DateTime Date, int Value);

public sealed record RecentLoginUserDto(Guid UserId, string UserName, string? Email, string? ClientId, string? ClientName, DateTime LoginAtUtc);

public sealed record DashboardOperationalSignalDto(string Key, string Label, int? Value, bool IsVisible, string? Description);

public sealed record DashboardOperationalSignalsDto(
    DashboardOperationalSignalDto? FailedLogins24h,
    DashboardOperationalSignalDto? ExpiringRefreshTokens7d,
    DashboardOperationalSignalDto? ClientConfigurationWarnings,
    DashboardOperationalSignalDto? ReviewableAuditEvents24h);

public sealed record DashboardSummaryDto(
    DashboardMetricDto Users,
    DashboardMetricDto Roles,
    DashboardMetricDto Permissions,
    DashboardMetricDto Clients,
    IReadOnlyList<DashboardTrendPointDto> LoginTrend,
    IReadOnlyList<DashboardTrendPointDto> TokenIssueTrend,
    IReadOnlyList<RecentLoginUserDto> RecentLoginUsers,
    DateTime GeneratedAtUtc,
    DashboardOperationalSignalsDto? OperationalSignals = null);

public sealed record UserGroupMemberDto(
    Guid UserId,
    string UserName,
    string Email,
    bool IsActive,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<string> Roles);

public sealed record UserGroupDto(
    string Id,
    string GroupName,
    string GroupType,
    int MemberCount,
    int PermissionCount,
    string RoleSummary,
    string PermissionSummary,
    IReadOnlyList<UserGroupMemberDto> Members);

public sealed record RefreshTokenRecordDto(
    Guid Id,
    string TokenPreview,
    Guid UserId,
    string UserName,
    string? UserEmail,
    string ClientId,
    string ClientName,
    IReadOnlyList<string> Scopes,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc)
{
    public string ScopesDisplay => Scopes.Count == 0
        ? "未配置"
        : string.Join("、", Scopes);

    public string RevokedStatusText => RevokedAtUtc.HasValue
        ? RevokedAtUtc.Value.ToLocalTime().ToString("MM-dd HH:mm")
        : ExpiresAtUtc <= DateTime.UtcNow ? "已过期" : "有效";
}

public sealed record PortalMenuItemDto(
    Guid Id,
    int Order,
    string Key,
    string Title,
    string Section,
    string Route,
    string IconKey,
    bool IsEnabled,
    bool IsImplemented,
    IReadOnlyList<string> RequiredPermissions,
    bool HasAccess,
    string Description,
    DateTime UpdatedAtUtc)
{
    public string RequiredPermissionsDisplay => RequiredPermissions.Count == 0
        ? "当前菜单对所有已登录用户可见。"
        : string.Join(" / ", RequiredPermissions);

    public string EnabledStatusText => IsEnabled ? "已启用" : "已停用";

    public bool IsDisabled => !IsEnabled;

    public string ImplementationStatusText => IsImplemented ? "已实现" : "待补全";

    public bool IsNotImplemented => !IsImplemented;

    public string AccessStatusText => HasAccess ? "当前账号可见" : "当前账号无权限";

    public bool IsAvailable => IsEnabled && IsImplemented;

    public string UpdatedAtDisplay => UpdatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public sealed record SavePortalMenuRequestDto(
    string Title,
    string Section,
    bool IsEnabled,
    IReadOnlyList<string> RequiredPermissions,
    string Description);

public sealed record ReorderPortalMenusRequestDto(IReadOnlyList<string> Keys);

public sealed record AuditLogEntryDto(
    string Id,
    string Category,
    string Action,
    string Actor,
    string Target,
    string Outcome,
    string Details,
    DateTime OccurredAtUtc)
{
    public string OccurredAtDisplay => OccurredAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

public sealed record SystemSettingsSummaryDto(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    bool RefreshTokenGrantAdvertised,
    bool SeedEnabled,
    int UserCount,
    int RoleCount,
    int PermissionCount,
    int ClientCount,
    string DefaultClientId,
    DateTime ServerTimeUtc);

public sealed record AuthorizePreviewClientDto(
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientName")] string ClientName,
    [property: JsonPropertyName("clientType")] string ClientType);

public sealed record AuthorizePreviewUserDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("email")] string Email);

public sealed record AuthorizePreviewDto(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("client")] AuthorizePreviewClientDto Client,
    [property: JsonPropertyName("user")] AuthorizePreviewUserDto User,
    [property: JsonPropertyName("scopes")] IReadOnlyList<string> Scopes,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("approve_uri")] string ApproveUri,
    [property: JsonPropertyName("deny_uri")] string DenyUri);
