using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumine.AuthPortal.Models;
using Lumine.AuthPortal.Services;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views.Pages;

public partial class ProfileCenterPageView : UserControl
{
    public ProfileCenterPageView()
    {
        InitializeComponent();
    }

    private async void OnCopyApiKeyItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ApiKeyDto { PlainTextKey: string text } })
        {
            return;
        }

        if (DataContext is ProfileCenterPageViewModel viewModel)
        {
            viewModel.StatusMessage = await ClipboardService.TrySetTextAsync(this, text)
                ? "API-KEY 已复制到剪贴板，请妥善保管。"
                : "复制失败，请检查浏览器剪贴板权限后重试。";
        }
    }
}
