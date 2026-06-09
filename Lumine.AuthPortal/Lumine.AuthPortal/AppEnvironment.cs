using System;

namespace Lumine.AuthPortal;

public static class AppEnvironment
{
    public const string DefaultServerBaseUrl = "http://localhost:5115";

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

        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            ? uri.ToString().TrimEnd('/')
            : DefaultServerBaseUrl;
    }
}