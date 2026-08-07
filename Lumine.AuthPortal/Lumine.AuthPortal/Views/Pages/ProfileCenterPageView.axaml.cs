using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumine.AuthPortal.Models;
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

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(text);
        if (DataContext is ProfileCenterPageViewModel viewModel)
        {
            viewModel.StatusMessage = "API-KEY 已复制到剪贴板，请妥善保管。";
        }
    }
}
