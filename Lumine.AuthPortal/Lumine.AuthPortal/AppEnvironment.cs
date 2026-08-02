using System;

namespace Lumine.AuthPortal;

public static class AppEnvironment
{
    public const string DefaultServerBaseUrl = "http://localhost:13255";
    public const string DefaultOidcClientId = "lumine-demo-client";
    public const string DefaultOidcRedirectUri = "http://localhost:5173/signin-oidc";
    public const string DefaultOidcScope = "openid profile email roles permissions";

    private static string _serverBaseUrl = DefaultServerBaseUrl;

    public static string ServerBaseUrl
    {
        get => _serverBaseUrl;
        set => _serverBaseUrl = Normalize(value);
    }

    public static void Configure(string? serverBaseUrl)
    {
        _serverBaseUrl = Normalize(serverBaseUrl);
    }

    public static string EnsureTrailingSlash(string? serverBaseUrl)
    {
        return Normalize(serverBaseUrl) + "/";
    }

    public static string Normalize(string? serverBaseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(serverBaseUrl)
            ? DefaultServerBaseUrl
            : serverBaseUrl.Trim();

        return TryNormalize(normalized, out var result, out _)
            ? result
            : DefaultServerBaseUrl;
    }

    public static bool TryNormalize(string? serverBaseUrl, out string normalized, out string errorMessage)
    {
        normalized = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(serverBaseUrl))
        {
            errorMessage = "后端地址不能为空。";
            return false;
        }

        if (!Uri.TryCreate(serverBaseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            errorMessage = "后端地址必须是包含主机名的 http 或 https 地址。";
            return false;
        }

        normalized = uri.ToString().TrimEnd('/');
        return true;
    }
}
