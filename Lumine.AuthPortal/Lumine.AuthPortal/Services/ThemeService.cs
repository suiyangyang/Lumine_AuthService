using System;
using System.Collections.Generic;
using System.IO;
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
            "Fluent",
            "跟随 FluentTheme 的清爽中性色界面，适合日常后台操作。",
            "#EEF3F9",
            "#FFFFFF",
            "#2563EB",
            "#0F172A"),
        new(
            PortalThemeMode.LumineDark,
            "lumine-dark",
            "Lumine Dark",
            "保留当前深色主体视觉，更强调信息分层与夜间使用体验。",
            "#0B1220",
            "#111827",
            "#60A5FA",
            "#F9FAFB")
    ];

    private readonly string _settingsFilePath;
    private ResourceDictionary? _activeThemePalette;

    public ThemeService()
    {
        _settingsFilePath = BuildSettingsFilePath();
        SelectedTheme = LoadSavedTheme();
    }

    public IReadOnlyList<ThemeOptionItem> ThemeOptions => ThemeOptionsData;

    [ObservableProperty]
    private PortalThemeMode _selectedTheme;

    public string CurrentThemeLabel => GetThemeOption(SelectedTheme).DisplayName;

    public bool IsFluentThemeSelected => SelectedTheme == PortalThemeMode.Fluent;

    public bool IsLumineDarkThemeSelected => SelectedTheme == PortalThemeMode.LumineDark;

    public void ApplyCurrentTheme()
    {
        ApplyTheme(SelectedTheme, persist: false);
    }

    public void ApplyTheme(PortalThemeMode mode, bool persist = true)
    {
        SelectedTheme = mode;
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = mode == PortalThemeMode.Fluent
                ? ThemeVariant.Default
                : ThemeVariant.Dark;

            var mergedDictionaries = app.Resources.MergedDictionaries;
            if (_activeThemePalette != null)
            {
                mergedDictionaries.Remove(_activeThemePalette);
            }

            _activeThemePalette = BuildPalette(mode);
            mergedDictionaries.Add(_activeThemePalette);
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

    private ResourceDictionary BuildPalette(PortalThemeMode mode)
    {
        var palette = new ResourceDictionary
        {
            [ThemeResourcesKey] = ThemeResourcesKey
        };

        if (mode == PortalThemeMode.LumineDark)
        {
            SetBrushes(palette, new Dictionary<string, string>
            {
                ["AppBackgroundBrush"] = "#0B1220",
                ["SidebarBackgroundBrush"] = "#111827",
                ["TopbarBackgroundBrush"] = "#111827",
                ["SurfacePanelBrush"] = "#111827",
                ["SurfaceRaisedBrush"] = "#1F2937",
                ["SurfaceInsetBrush"] = "#0F172A",
                ["SurfaceSoftBrush"] = "#172132",
                ["BorderSubtleBrush"] = "#1F2937",
                ["BorderStrongBrush"] = "#374151",
                ["TextPrimaryBrush"] = "#F9FAFB",
                ["TextSecondaryBrush"] = "#D1D5DB",
                ["TextMutedBrush"] = "#94A3B8",
                ["SurfaceHoverBrush"] = "#172033",
                ["SurfacePressedBrush"] = "#1D2A44",
                ["SurfaceSelectedBrush"] = "#1F2937",
                ["SurfaceSelectedHoverBrush"] = "#243041",
                ["SurfaceSelectedPressedBrush"] = "#293548",
                ["BorderHoverBrush"] = "#4B5563",
                ["BorderActiveBrush"] = "#60A5FA",
                ["BorderActiveStrongBrush"] = "#93C5FD",
                ["TextOnAccentBrush"] = "#FFFFFF",
                ["TextOnSelectedBrush"] = "#F9FAFB",
                ["TextAccentSoftBrush"] = "#BFDBFE",
                ["StatusInfoBrush"] = "#BFDBFE"
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
                ["StatusInfoBrush"] = "#1D4ED8"
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
            if (!File.Exists(_settingsFilePath))
            {
                return PortalThemeMode.Fluent;
            }

            var json = File.ReadAllText(_settingsFilePath);
            var data = JsonSerializer.Deserialize<ThemeSettingsState>(json);
            return Enum.TryParse<PortalThemeMode>(data?.SelectedTheme, true, out var parsedTheme)
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
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = JsonSerializer.Serialize(new ThemeSettingsState(mode.ToString()), new JsonSerializerOptions
            {
                WriteIndented = true
            });

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
