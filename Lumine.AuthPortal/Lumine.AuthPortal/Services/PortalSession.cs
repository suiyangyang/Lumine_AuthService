using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumine.AuthPortal.Models;

namespace Lumine.AuthPortal.Services;

public partial class PortalSession : ObservableObject
{
    [ObservableProperty]
    private string _accessToken = string.Empty;

    [ObservableProperty]
    private string? _idToken;

    [ObservableProperty]
    private string _userName = "访客";

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _roles = Array.Empty<string>();

    [ObservableProperty]
    private IReadOnlyList<string> _permissions = Array.Empty<string>();

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    public void ApplyLogin(LoginResponseDto response)
    {
        AccessToken = response.AccessToken;
        IdToken = response.IdToken;
        UserName = response.User?.UserName ?? "已登录用户";
        Email = response.User?.Email ?? string.Empty;
        Roles = response.User?.Roles ?? Array.Empty<string>();
        Permissions = response.User?.Permissions ?? Array.Empty<string>();
        OnPropertyChanged(nameof(IsAuthenticated));
    }

    public void Clear()
    {
        AccessToken = string.Empty;
        IdToken = null;
        UserName = "访客";
        Email = string.Empty;
        Roles = Array.Empty<string>();
        Permissions = Array.Empty<string>();
        OnPropertyChanged(nameof(IsAuthenticated));
    }

    public bool HasPermission(string? permission)
    {
        return string.IsNullOrWhiteSpace(permission)
            || Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
