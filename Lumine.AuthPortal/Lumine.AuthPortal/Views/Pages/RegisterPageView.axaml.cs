using Avalonia.Controls;
using Avalonia.Input;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views.Pages;

public partial class RegisterPageView : UserControl
{
    public RegisterPageView()
    {
        InitializeComponent();
    }

    private void RegisterInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is RegisterPageViewModel viewModel && viewModel.RegisterCommand.CanExecute(null))
        {
            viewModel.RegisterCommand.Execute(null);
            e.Handled = true;
        }
    }
}
