using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views.Pages;

public partial class UserGroupsManagementPageView : UserControl
{
    public UserGroupsManagementPageView()
    {
        InitializeComponent();
    }

    private void SearchKeywordTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is UserGroupsManagementPageViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ToggleSearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not UserGroupsManagementPageViewModel viewModel || !viewModel.IsSearchVisible)
            {
                return;
            }

            SearchKeywordTextBox.Focus();
            SearchKeywordTextBox.CaretIndex = SearchKeywordTextBox.Text?.Length ?? 0;
        }, DispatcherPriority.Background);
    }
}
