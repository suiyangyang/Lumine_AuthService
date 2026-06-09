using Avalonia.Controls;
using Avalonia.Input;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views.Pages;

public partial class UsersManagementPageView : UserControl
{
    public UsersManagementPageView()
    {
        InitializeComponent();
    }

    private void SearchKeywordTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is UsersManagementPageViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
