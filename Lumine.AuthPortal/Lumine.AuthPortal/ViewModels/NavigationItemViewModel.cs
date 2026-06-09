using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using System.ComponentModel;

namespace Lumine.AuthPortal.ViewModels;

public partial class NavigationSectionViewModel : ObservableObject
{
    public NavigationSectionViewModel(string title, string iconKey, IReadOnlyList<NavigationItemViewModel> items, bool isExpanded = true)
    {
        Title = title;
        IconKey = iconKey;
        IconData = NavigationIconData.Get(iconKey);
        Items = items;
        IsExpanded = isExpanded;

        foreach (var item in Items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    public string Title { get; }

    public string IconKey { get; }

    public StreamGeometry IconData { get; }

    public IReadOnlyList<NavigationItemViewModel> Items { get; }

    public StreamGeometry ExpandIconData => NavigationIconData.Get(IsExpanded ? "chevron-down" : "chevron-right");

    public int VisibleItemCount => Items.Count(item => item.IsVisible);

    public bool IsActiveSection => Items.Any(item => item.IsSelected);

    public double ChildrenOpacity => IsExpanded ? 1d : 0d;

    public double ChildrenMaxHeight => IsExpanded ? Math.Max(VisibleItemCount * 56d + 12d, 68d) : 0d;

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpandIconData));
        OnPropertyChanged(nameof(ChildrenOpacity));
        OnPropertyChanged(nameof(ChildrenMaxHeight));
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(NavigationItemViewModel.IsVisible) or nameof(NavigationItemViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(VisibleItemCount));
            OnPropertyChanged(nameof(IsActiveSection));
            OnPropertyChanged(nameof(ChildrenMaxHeight));
        }
    }
}

public partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(string key, string title, string iconKey, params string[] permissions)
    {
        Key = key;
        Title = title;
        IconKey = iconKey;
        IconData = NavigationIconData.Get(iconKey);
        Permissions = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Key { get; }

    public string Title { get; }

    public string IconKey { get; }

    public StreamGeometry IconData { get; }

    public IReadOnlyList<string> Permissions { get; }

    public bool RequiresPermission => Permissions.Count > 0;

    public string PermissionHint => string.Join(" / ", Permissions);

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isSelected;
}

public static class NavigationIconData
{
    private static readonly Dictionary<string, StreamGeometry> Icons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = Parse("M3 13H11V3H3V13ZM13 21H21V11H13V21ZM3 21H11V15H3V21ZM13 3V9H21V3H13Z"),
        ["login"] = Parse("M10 17L15 12L10 7V10H3V14H10V17ZM12 3H19C20.1 3 21 3.9 21 5V19C21 20.1 20.1 21 19 21H12V19H19V5H12V3Z"),
        ["register"] = Parse("M4 4H14V6H4V4ZM4 9H14V11H4V9ZM4 14H11V16H4V14ZM17 13V16H20V18H17V21H15V18H12V16H15V13H17ZM16.5 4L20 7.5L18.6 8.9L15.1 5.4L16.5 4Z"),
        ["users"] = Parse("M12 12C14.2 12 16 10.2 16 8S14.2 4 12 4 8 5.8 8 8 9.8 12 12 12ZM4 20C4 16.7 7.6 14 12 14S20 16.7 20 20V21H4V20Z"),
        ["roles"] = Parse("M12 2L20 5V11C20 16 16.6 20.4 12 22C7.4 20.4 4 16 4 11V5L12 2ZM10.8 15.4L17 9.2L15.6 7.8L10.8 12.6L8.4 10.2L7 11.6L10.8 15.4Z"),
        ["permissions"] = Parse("M7 14C4.8 14 3 12.2 3 10S4.8 6 7 6C8.5 6 9.8 6.8 10.5 8H21V11H18V14H15V11H10.5C9.8 12.2 8.5 14 7 14ZM7 11.5C7.8 11.5 8.5 10.8 8.5 10S7.8 8.5 7 8.5 5.5 9.2 5.5 10 6.2 11.5 7 11.5Z"),
        ["user-groups"] = Parse("M8 11C9.7 11 11 9.7 11 8S9.7 5 8 5 5 6.3 5 8 6.3 11 8 11ZM16 11C17.7 11 19 9.7 19 8S17.7 5 16 5 13 6.3 13 8 14.3 11 16 11ZM8 13C5.3 13 3 14.3 3 16V18H13V16C13 14.3 10.7 13 8 13ZM16 13C15.4 13 14.8 13.1 14.2 13.2C15.3 14 16 15 16 16V18H21V16C21 14.3 18.7 13 16 13Z"),
        ["clients"] = Parse("M12 2C17.5 2 22 6.5 22 12S17.5 22 12 22 2 17.5 2 12 6.5 2 12 2ZM4.3 13H8.1C8.3 15.1 9 17 10 18.4C7 17.7 4.8 15.6 4.3 13ZM15.9 13H19.7C19.2 15.6 17 17.7 14 18.4C15 17 15.7 15.1 15.9 13ZM10.1 13H13.9C13.6 16.1 12.8 18.5 12 18.5S10.4 16.1 10.1 13ZM4.3 11C4.8 8.4 7 6.3 10 5.6C9 7 8.3 8.9 8.1 11H4.3ZM10.1 11C10.4 7.9 11.2 5.5 12 5.5S13.6 7.9 13.9 11H10.1ZM15.9 11C15.7 8.9 15 7 14 5.6C17 6.3 19.2 8.4 19.7 11H15.9Z"),
        ["consent"] = Parse("M9 16.2L4.8 12L3.4 13.4L9 19L21 7L19.6 5.6L9 16.2Z"),
        ["tokens"] = Parse("M7 14C4.8 14 3 12.2 3 10S4.8 6 7 6C8.5 6 9.8 6.8 10.5 8H14L16 10L18 8L21 11L19.6 12.4L18 10.8L16 12.8L13.2 10H10.5C9.8 12.2 8.5 14 7 14ZM7 11.5C7.8 11.5 8.5 10.8 8.5 10S7.8 8.5 7 8.5 5.5 9.2 5.5 10 6.2 11.5 7 11.5Z"),
        ["oidc"] = Parse("M12 2L15 8L21 9L16.5 13.5L17.5 20L12 17L6.5 20L7.5 13.5L3 9L9 8L12 2ZM12 6.4L10.4 9.7L6.8 10.2L9.4 12.8L8.8 16.4L12 14.7L15.2 16.4L14.6 12.8L17.2 10.2L13.6 9.7L12 6.4Z"),
        ["menus"] = Parse("M4 5H20V7H4V5ZM4 11H20V13H4V11ZM4 17H20V19H4V17Z"),
        ["settings"] = Parse("M19.4 13.5C19.5 13 19.5 12.5 19.5 12S19.5 11 19.4 10.5L21.5 8.9L19.5 5.5L17 6.5C16.2 5.9 15.4 5.4 14.5 5.1L14.1 2.5H10.1L9.7 5.1C8.8 5.4 8 5.9 7.2 6.5L4.7 5.5L2.7 8.9L4.8 10.5C4.7 11 4.7 11.5 4.7 12S4.7 13 4.8 13.5L2.7 15.1L4.7 18.5L7.2 17.5C8 18.1 8.8 18.6 9.7 18.9L10.1 21.5H14.1L14.5 18.9C15.4 18.6 16.2 18.1 17 17.5L19.5 18.5L21.5 15.1L19.4 13.5ZM12.1 15.5C10.2 15.5 8.6 13.9 8.6 12S10.2 8.5 12.1 8.5 15.6 10.1 15.6 12 14 15.5 12.1 15.5Z"),
        ["audit"] = Parse("M6 2H16L20 6V22H6V2ZM15 3.5V7H18.5L15 3.5ZM8 10H18V12H8V10ZM8 14H18V16H8V14ZM8 18H14V20H8V18Z"),
        ["overview"] = Parse("M4 4H20V8H4V4ZM4 10H11V20H4V10ZM13 10H20V20H13V10Z"),
        ["identity"] = Parse("M12 12C14.2 12 16 10.2 16 8S14.2 4 12 4 8 5.8 8 8 9.8 12 12 12ZM4 20C4 16.7 7.6 14 12 14S20 16.7 20 20V21H4V20Z"),
        ["auth"] = Parse("M12 2C9.2 2 7 4.2 7 7V10H6C4.9 10 4 10.9 4 12V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V12C20 10.9 19.1 10 18 10H17V7C17 4.2 14.8 2 12 2ZM9 10V7C9 5.3 10.3 4 12 4S15 5.3 15 7V10H9Z"),
        ["system"] = Parse("M4 6H20V8H4V6ZM4 11H20V13H4V11ZM4 16H20V18H4V16Z"),
        ["chevron-down"] = Parse("M7.4 8.6L12 13.2L16.6 8.6L18 10L12 16L6 10L7.4 8.6Z"),
        ["chevron-right"] = Parse("M9.4 6L15.4 12L9.4 18L8 16.6L12.6 12L8 7.4L9.4 6Z")
    };

    public static StreamGeometry Get(string key)
    {
        return Icons.TryGetValue(key, out var icon)
            ? icon
            : Icons["dashboard"];
    }

    private static StreamGeometry Parse(string data)
    {
        return StreamGeometry.Parse(data);
    }
}
