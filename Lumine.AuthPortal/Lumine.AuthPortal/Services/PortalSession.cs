using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumine.AuthPortal.Models;

namespace Lumine.AuthPortal.Services;

public partial class PortalSession : ObservableObject
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public PortalSession()
    {
        _settingsFilePath = BuildSettingsFilePath();
        Restore();
    }

    [ObservableProperty]
    private string _accessToken = string.Empty;

    [ObservableProperty]
    private string? _idToken;

    [ObservableProperty]
    private DateTime? _accessTokenExpiresAtUtc;

    [ObservableProperty]
    private string _userName = "访客";

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _roles = Array.Empty<string>();

    [ObservableProperty]
    private IReadOnlyList<string> _permissions = Array.Empty<string>();

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken)
        && (!AccessTokenExpiresAtUtc.HasValue || AccessTokenExpiresAtUtc.Value > DateTime.UtcNow);

    partial void OnAccessTokenChanged(string value)
    {
        OnPropertyChanged(nameof(IsAuthenticated));
    }

    partial void OnAccessTokenExpiresAtUtcChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(IsAuthenticated));
    }

    public void ApplyLogin(LoginResponseDto response)
    {
        AccessToken = response.AccessToken;
        IdToken = response.IdToken;
        AccessTokenExpiresAtUtc = response.ExpiresIn > 0
            ? DateTime.UtcNow.AddSeconds(response.ExpiresIn)
            : null;
        UserName = response.User?.UserName ?? "已登录用户";
        Email = response.User?.Email ?? string.Empty;
        Roles = response.User?.Roles ?? Array.Empty<string>();
        Permissions = response.User?.Permissions ?? Array.Empty<string>();
        Persist();
    }

    public void Clear()
    {
        AccessToken = string.Empty;
        IdToken = null;
        AccessTokenExpiresAtUtc = null;
        UserName = "访客";
        Email = string.Empty;
        Roles = Array.Empty<string>();
        Permissions = Array.Empty<string>();
        ClearPersistedState();
    }

    public bool HasPermission(string? permission)
    {
        return string.IsNullOrWhiteSpace(permission)
            || Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    private void Restore()
    {
        try
        {
            var payload = LoadPersistedState();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            var state = JsonSerializer.Deserialize<PersistedPortalSessionState>(payload, _jsonOptions);
            if (state == null)
            {
                return;
            }

            if (state.AccessTokenExpiresAtUtc.HasValue && state.AccessTokenExpiresAtUtc.Value <= DateTime.UtcNow)
            {
                ClearPersistedState();
                return;
            }

            AccessToken = state.AccessToken ?? string.Empty;
            IdToken = state.IdToken;
            AccessTokenExpiresAtUtc = state.AccessTokenExpiresAtUtc;
            UserName = string.IsNullOrWhiteSpace(state.UserName) ? "访客" : state.UserName;
            Email = state.Email ?? string.Empty;
            Roles = state.Roles ?? Array.Empty<string>();
            Permissions = state.Permissions ?? Array.Empty<string>();
        }
        catch
        {
            ClearPersistedState();
        }
    }

    private void Persist()
    {
        try
        {
            var payload = JsonSerializer.Serialize(new PersistedPortalSessionState(
                AccessToken,
                IdToken,
                AccessTokenExpiresAtUtc,
                UserName,
                Email,
                Roles,
                Permissions), _jsonOptions);

            if (OperatingSystem.IsBrowser())
            {
                BrowserPortalSessionStorage.Save(payload);
                return;
            }

            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_settingsFilePath, payload);
        }
        catch
        {
            // Ignore persistence failures and keep the in-memory session state.
        }
    }

    private void ClearPersistedState()
    {
        try
        {
            if (OperatingSystem.IsBrowser())
            {
                BrowserPortalSessionStorage.Clear();
                return;
            }

            if (File.Exists(_settingsFilePath))
            {
                File.Delete(_settingsFilePath);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private string? LoadPersistedState()
    {
        if (OperatingSystem.IsBrowser())
        {
            return BrowserPortalSessionStorage.Load();
        }

        return File.Exists(_settingsFilePath)
            ? File.ReadAllText(_settingsFilePath)
            : null;
    }

    private static string BuildSettingsFilePath()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lumine",
            "AuthPortal");

        return Path.Combine(baseDirectory, "session.json");
    }

    private sealed record PersistedPortalSessionState(
        string AccessToken,
        string? IdToken,
        DateTime? AccessTokenExpiresAtUtc,
        string UserName,
        string Email,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions);
}

[SupportedOSPlatform("browser")]
internal static partial class BrowserPortalSessionStorage
{
    [JSImport("globalThis.lumineAuthPortalSession.load")]
    internal static partial string? Load();

    [JSImport("globalThis.lumineAuthPortalSession.save")]
    internal static partial void Save(string payload);

    [JSImport("globalThis.lumineAuthPortalSession.clear")]
    internal static partial void Clear();
}
