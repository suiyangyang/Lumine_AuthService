using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumine.AuthPortal.Services;
using Lumine.AuthPortal.ViewModels;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public ICommand? ToggleSectionCommand => (DataContext as MainViewModel)?.ToggleSectionCommand;

    public ICommand? NavigateCommand => (DataContext as MainViewModel)?.NavigateCommand;

    private async void OnCopyApiKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string text } || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (DataContext is MainViewModel { CurrentPage: ProfileCenterPageViewModel viewModel })
        {
            viewModel.StatusMessage = await ClipboardService.TrySetTextAsync(this, text)
                ? "API-KEY 已复制到剪贴板，请妥善保管。"
                : "复制失败，请检查浏览器剪贴板权限后重试。";
        }
    }
}
