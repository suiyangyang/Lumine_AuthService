using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace Lumine.AuthPortal.ViewModels;

public partial class NavigationSectionViewModel : ObservableObject
{
    public NavigationSectionViewModel(string title, string iconGlyph, IReadOnlyList<NavigationItemViewModel> items, bool isExpanded = true)
    {
        Title = title;
        IconGlyph = iconGlyph;
        Items = items;
        IsExpanded = isExpanded;

        foreach (var item in Items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    public string Title { get; }

    public string IconGlyph { get; }

    public IReadOnlyList<NavigationItemViewModel> Items { get; }

    public string ExpandGlyph => IsExpanded ? "▾" : "▸";

    public int VisibleItemCount => Items.Count(item => item.IsVisible);

    public bool IsActiveSection => Items.Any(item => item.IsSelected);

    public double ChildrenOpacity => IsExpanded ? 1d : 0d;

    public double ChildrenMaxHeight => IsExpanded ? Math.Max(VisibleItemCount * 56d + 12d, 68d) : 0d;

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpandGlyph));
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
    public NavigationItemViewModel(string key, string title, string iconGlyph, params string[] permissions)
    {
        Key = key;
        Title = title;
        IconGlyph = iconGlyph;
        Permissions = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Key { get; }

    public string Title { get; }

    public string IconGlyph { get; }

    public IReadOnlyList<string> Permissions { get; }

    public bool RequiresPermission => Permissions.Count > 0;

    public string PermissionHint => string.Join(" / ", Permissions);

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isSelected;
}
