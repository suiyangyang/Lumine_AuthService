using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views.Pages;

public partial class ClientsManagementPageView : UserControl
{
    public ClientsManagementPageView()
    {
        InitializeComponent();
    }

    private async void OnCopyTextClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string text } || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(text);

        if (DataContext is ClientsManagementPageViewModel viewModel)
        {
            viewModel.StatusMessage = "内容已复制到剪贴板。";
        }
    }

    private void ClientSearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is ClientsManagementPageViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
