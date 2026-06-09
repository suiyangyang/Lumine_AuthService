using System.Windows.Input;
using Avalonia.Controls;
using Lumine.AuthPortal.ViewModels;

namespace Lumine.AuthPortal.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public ICommand? ToggleSectionCommand => (DataContext as MainViewModel)?.ToggleSectionCommand;

    public ICommand? NavigateCommand => (DataContext as MainViewModel)?.NavigateCommand;
}
