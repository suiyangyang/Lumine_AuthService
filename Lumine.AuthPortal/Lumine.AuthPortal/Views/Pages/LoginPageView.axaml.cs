using Avalonia.Controls;
using Avalonia.Input;
using Lumine.AuthPortal.ViewModels.Pages;

namespace Lumine.AuthPortal.Views.Pages;

public partial class LoginPageView : UserControl
{
    public LoginPageView()
    {
        InitializeComponent();
    }

    private void LoginInput_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is LoginPageViewModel viewModel && viewModel.LoginCommand.CanExecute(null))
        {
            viewModel.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }
}
