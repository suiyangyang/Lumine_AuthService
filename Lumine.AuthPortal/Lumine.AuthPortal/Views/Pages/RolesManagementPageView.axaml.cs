using Avalonia.Controls;
using Avalonia.Input;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views.Pages;

public partial class RolesManagementPageView : UserControl
{
    public RolesManagementPageView()
    {
        InitializeComponent();
    }

    private void RoleSearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is RolesManagementPageViewModel viewModel && viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
