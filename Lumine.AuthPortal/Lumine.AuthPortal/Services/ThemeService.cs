using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumine.AuthPortal.Services;

public enum PortalThemeMode
{
    Fluent = 0,
    LumineDark = 1
}

public sealed record ThemeOptionItem(
    PortalThemeMode Mode,
    string Key,
    string DisplayName,
    string Description,
    string PreviewBackground,
    string PreviewSurface,
    string PreviewAccent,
    string PreviewText);

public partial class ThemeService : ObservableObject
{
    private const string ThemeResourcesKey = "PortalThemeResources";

    private static readonly IReadOnlyList<ThemeOptionItem> ThemeOptionsData =
    [
        new(
            PortalThemeMode.Fluent,
            "fluent",
            "Fluent Light",
            "显式使用浅色 Fluent 风格，保持后台信息密度与日间可读性。",
            "#EEF3F9",
            "#FFFFFF",
            "#0E639C",
            "#0F172A"),
        new(
            PortalThemeMode.LumineDark,
            "lumine-dark",
            "VS Code Dark",
            "以 VS Code Dark 为参考的深色后台主题，强调分层、对比和长时间使用舒适度。",
            "#1E1E1E",
            "#252526",
            "#0E639C",
            "#CCCCCC")
    ];

    private readonly string _settingsFilePath;
    private bool _hasLoadedPersistedTheme;

    public ThemeService()
    {
        _settingsFilePath = BuildSettingsFilePath();
        SelectedTheme = PortalThemeMode.Fluent;
    }

    public IReadOnlyList<ThemeOptionItem> ThemeOptions => ThemeOptionsData;

    [ObservableProperty]
    private PortalThemeMode _selectedTheme;

    public string CurrentThemeLabel => GetThemeOption(SelectedTheme).DisplayName;

    public bool IsFluentThemeSelected => SelectedTheme == PortalThemeMode.Fluent;

    public bool IsLumineDarkThemeSelected => SelectedTheme == PortalThemeMode.LumineDark;

    public void ApplyCurrentTheme()
    {
        EnsureThemeLoaded();
        ApplyTheme(SelectedTheme, persist: false);
    }

    public void ApplyTheme(PortalThemeMode mode, bool persist = true)
    {
        _hasLoadedPersistedTheme = true;
        SelectedTheme = mode;
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = mode == PortalThemeMode.Fluent
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
            var palette = BuildPalette(mode);
            foreach (var entry in palette)
            {
                app.Resources[entry.Key] = entry.Value;
            }
        }

        OnPropertyChanged(nameof(CurrentThemeLabel));
        OnPropertyChanged(nameof(IsFluentThemeSelected));
        OnPropertyChanged(nameof(IsLumineDarkThemeSelected));

        if (persist)
        {
            SaveTheme(mode);
        }
    }

    public void ToggleTheme()
    {
        ApplyTheme(SelectedTheme == PortalThemeMode.Fluent
            ? PortalThemeMode.LumineDark
            : PortalThemeMode.Fluent);
    }

    public ThemeOptionItem GetThemeOption(PortalThemeMode mode)
    {
        return ThemeOptionsData.First(option => option.Mode == mode);
    }

    partial void OnSelectedThemeChanged(PortalThemeMode value)
    {
        OnPropertyChanged(nameof(CurrentThemeLabel));
        OnPropertyChanged(nameof(IsFluentThemeSelected));
        OnPropertyChanged(nameof(IsLumineDarkThemeSelected));
    }

    private void EnsureThemeLoaded()
    {
        if (_hasLoadedPersistedTheme)
        {
            return;
        }

        SelectedTheme = LoadSavedTheme();
        _hasLoadedPersistedTheme = true;
    }

    private ResourceDictionary BuildPalette(PortalThemeMode mode)
    {
        var palette = new ResourceDictionary();

        if (mode == PortalThemeMode.LumineDark)
        {
            SetBrushes(palette, new Dictionary<string, string>
            {
                ["AppBackgroundBrush"] = "#1E1E1E",
                ["SidebarBackgroundBrush"] = "#252526",
                ["TopbarBackgroundBrush"] = "#252526",
                ["SurfacePanelBrush"] = "#252526",
                ["SurfaceRaisedBrush"] = "#2D2D30",
                ["SurfaceInsetBrush"] = "#1E1E1E",
                ["SurfaceSoftBrush"] = "#2A2D2E",
                ["BorderSubtleBrush"] = "#2D2D30",
                ["BorderStrongBrush"] = "#3C3C3C",
                ["TextPrimaryBrush"] = "#CCCCCC",
                ["TextSecondaryBrush"] = "#B8B8B8",
                ["TextMutedBrush"] = "#8C8C8C",
                ["SurfaceHoverBrush"] = "#2A2D2E",
                ["SurfacePressedBrush"] = "#37373D",
                ["SurfaceSelectedBrush"] = "#37373D",
                ["SurfaceSelectedHoverBrush"] = "#404040",
                ["SurfaceSelectedPressedBrush"] = "#45494E",
                ["BorderHoverBrush"] = "#5A5A5A",
                ["BorderActiveBrush"] = "#007ACC",
                ["BorderActiveStrongBrush"] = "#0E639C",
                ["TextOnAccentBrush"] = "#FFFFFF",
                ["TextOnSelectedBrush"] = "#FFFFFF",
                ["TextAccentSoftBrush"] = "#4FC1FF",
                ["StatusInfoBrush"] = "#4FC1FF",
                ["AccentBrush"] = "#0E639C",
                ["AccentHoverBrush"] = "#1177BB",
                ["AccentPressedBrush"] = "#094771",
                ["AccentBorderBrush"] = "#3794FF",
                ["AccentBorderStrongBrush"] = "#75BEFF",
                ["LinkBrush"] = "#4FC1FF",
                ["LinkHoverBrush"] = "#75BEFF",
                ["LinkPressedBrush"] = "#9CDCFE",
                ["SelectionBrush"] = "#264F78",
                ["DialogBackdropBrush"] = "#B3121212",
                ["DangerBannerBackgroundBrush"] = "#3A1D1D",
                ["DangerBannerBorderBrush"] = "#6B2D2D",
                ["DangerBannerForegroundBrush"] = "#F48771",
                ["SuccessBannerBackgroundBrush"] = "#163B2D",
                ["SuccessBannerBorderBrush"] = "#3FB950",
                ["SuccessBannerForegroundBrush"] = "#89D185",
                ["SuccessPillBackgroundBrush"] = "#163B2D",
                ["SuccessPillForegroundBrush"] = "#89D185",
                ["DangerPillBackgroundBrush"] = "#3A1D1D",
                ["DangerPillForegroundBrush"] = "#F48771",
                ["DangerSoftBackgroundBrush"] = "#3A1D1D",
                ["DangerSoftHoverBackgroundBrush"] = "#4A2525",
                ["DangerSoftPressedBackgroundBrush"] = "#5C2C2C",
                ["DangerSoftBorderBrush"] = "#7A3B3B",
                ["DangerSoftHoverBorderBrush"] = "#A65050",
                ["DangerSoftPressedBorderBrush"] = "#C75A5A",
                ["DangerSoftForegroundBrush"] = "#F48771",
                ["DangerSoftHoverForegroundBrush"] = "#FFB3A7",
                ["DangerSoftPressedForegroundBrush"] = "#FFD0C8",
                ["DangerSolidBackgroundBrush"] = "#C74E39",
                ["DangerSolidHoverBackgroundBrush"] = "#D16969",
                ["DangerSolidPressedBackgroundBrush"] = "#A93E2B",
                ["DangerSolidBorderBrush"] = "#F48771",
                ["DangerSolidHoverBorderBrush"] = "#FFB3A7",
                ["DangerSolidPressedBorderBrush"] = "#FFD0C8",
                ["SuccessBackgroundBrush"] = "#16825D",
                ["SuccessHoverBackgroundBrush"] = "#1F9D73",
                ["SuccessPressedBackgroundBrush"] = "#106B4E",
                ["SuccessBorderBrush"] = "#3FB950",
                ["SuccessHoverBorderBrush"] = "#73C991",
                ["SuccessPressedBorderBrush"] = "#A6E3B8",
                ["RoleTagBackgroundBrush"] = "#1F3A5F",
                ["RoleTagForegroundBrush"] = "#9CDCFE"
            });
        }
        else
        {
            SetBrushes(palette, new Dictionary<string, string>
            {
                ["AppBackgroundBrush"] = "#F3F6FB",
                ["SidebarBackgroundBrush"] = "#FFFFFF",
                ["TopbarBackgroundBrush"] = "#FFFFFF",
                ["SurfacePanelBrush"] = "#FFFFFF",
                ["SurfaceRaisedBrush"] = "#EEF3F8",
                ["SurfaceInsetBrush"] = "#F7F9FC",
                ["SurfaceSoftBrush"] = "#E8EEF6",
                ["BorderSubtleBrush"] = "#D5DEEA",
                ["BorderStrongBrush"] = "#C2CEDD",
                ["TextPrimaryBrush"] = "#0F172A",
                ["TextSecondaryBrush"] = "#334155",
                ["TextMutedBrush"] = "#64748B",
                ["SurfaceHoverBrush"] = "#E8EEF6",
                ["SurfacePressedBrush"] = "#DDE7F4",
                ["SurfaceSelectedBrush"] = "#DBEAFE",
                ["SurfaceSelectedHoverBrush"] = "#D3E4FB",
                ["SurfaceSelectedPressedBrush"] = "#C7DBF8",
                ["BorderHoverBrush"] = "#94A3B8",
                ["BorderActiveBrush"] = "#3B82F6",
                ["BorderActiveStrongBrush"] = "#2563EB",
                ["TextOnAccentBrush"] = "#FFFFFF",
                ["TextOnSelectedBrush"] = "#0F172A",
                ["TextAccentSoftBrush"] = "#1D4ED8",
                ["StatusInfoBrush"] = "#1D4ED8",
                ["AccentBrush"] = "#2563EB",
                ["AccentHoverBrush"] = "#1D4ED8",
                ["AccentPressedBrush"] = "#1E40AF",
                ["AccentBorderBrush"] = "#60A5FA",
                ["AccentBorderStrongBrush"] = "#BFDBFE",
                ["LinkBrush"] = "#2563EB",
                ["LinkHoverBrush"] = "#1D4ED8",
                ["LinkPressedBrush"] = "#1E40AF",
                ["SelectionBrush"] = "#BFDBFE",
                ["DialogBackdropBrush"] = "#AA020617",
                ["DangerBannerBackgroundBrush"] = "#2B1220",
                ["DangerBannerBorderBrush"] = "#7F1D1D",
                ["DangerBannerForegroundBrush"] = "#FFFFFF",
                ["SuccessBannerBackgroundBrush"] = "#ECFDF5",
                ["SuccessBannerBorderBrush"] = "#6EE7B7",
                ["SuccessBannerForegroundBrush"] = "#065F46",
                ["SuccessPillBackgroundBrush"] = "#0F766E",
                ["SuccessPillForegroundBrush"] = "#ECFDF5",
                ["DangerPillBackgroundBrush"] = "#7F1D1D",
                ["DangerPillForegroundBrush"] = "#FEF2F2",
                ["DangerSoftBackgroundBrush"] = "#FFF1F2",
                ["DangerSoftHoverBackgroundBrush"] = "#FFE4E6",
                ["DangerSoftPressedBackgroundBrush"] = "#FECDD3",
                ["DangerSoftBorderBrush"] = "#FCA5A5",
                ["DangerSoftHoverBorderBrush"] = "#FB7185",
                ["DangerSoftPressedBorderBrush"] = "#E11D48",
                ["DangerSoftForegroundBrush"] = "#B91C1C",
                ["DangerSoftHoverForegroundBrush"] = "#9F1239",
                ["DangerSoftPressedForegroundBrush"] = "#881337",
                ["DangerSolidBackgroundBrush"] = "#DC2626",
                ["DangerSolidHoverBackgroundBrush"] = "#B91C1C",
                ["DangerSolidPressedBackgroundBrush"] = "#991B1B",
                ["DangerSolidBorderBrush"] = "#F87171",
                ["DangerSolidHoverBorderBrush"] = "#FCA5A5",
                ["DangerSolidPressedBorderBrush"] = "#FECACA",
                ["SuccessBackgroundBrush"] = "#059669",
                ["SuccessHoverBackgroundBrush"] = "#047857",
                ["SuccessPressedBackgroundBrush"] = "#065F46",
                ["SuccessBorderBrush"] = "#34D399",
                ["SuccessHoverBorderBrush"] = "#6EE7B7",
                ["SuccessPressedBorderBrush"] = "#A7F3D0",
                ["RoleTagBackgroundBrush"] = "#312E81",
                ["RoleTagForegroundBrush"] = "#E0E7FF"
            });
        }

        return palette;
    }

    private static void SetBrushes(ResourceDictionary resources, IReadOnlyDictionary<string, string> colorMap)
    {
        foreach (var (key, value) in colorMap)
        {
            resources[key] = new SolidColorBrush(Color.Parse(value));
        }
    }

    private PortalThemeMode LoadSavedTheme()
    {
        try
        {
            if (OperatingSystem.IsBrowser())
            {
                var payload = BrowserPortalThemeStorage.Load();
                var data = string.IsNullOrWhiteSpace(payload)
                    ? null
                    : JsonSerializer.Deserialize<ThemeSettingsState>(payload);

                return Enum.TryParse<PortalThemeMode>(data?.SelectedTheme, true, out var browserTheme)
                    ? browserTheme
                    : PortalThemeMode.Fluent;
            }

            if (!File.Exists(_settingsFilePath))
            {
                return PortalThemeMode.Fluent;
            }

            var json = File.ReadAllText(_settingsFilePath);
            var state = JsonSerializer.Deserialize<ThemeSettingsState>(json);
            return Enum.TryParse<PortalThemeMode>(state?.SelectedTheme, true, out var parsedTheme)
                ? parsedTheme
                : PortalThemeMode.Fluent;
        }
        catch
        {
            return PortalThemeMode.Fluent;
        }
    }

    private void SaveTheme(PortalThemeMode mode)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new ThemeSettingsState(mode.ToString()), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            if (OperatingSystem.IsBrowser())
            {
                BrowserPortalThemeStorage.Save(payload);
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
            // Ignore persistence failures and keep the in-memory theme selection.
        }
    }

    private static string BuildSettingsFilePath()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lumine",
            "AuthPortal");

        return Path.Combine(baseDirectory, "ui-settings.json");
    }

    private sealed record ThemeSettingsState(string SelectedTheme);
}

[SupportedOSPlatform("browser")]
internal static partial class BrowserPortalThemeStorage
{
    [JSImport("globalThis.lumineAuthPortalTheme.load")]
    internal static partial string? Load();

    [JSImport("globalThis.lumineAuthPortalTheme.save")]
    internal static partial void Save(string payload);
}
