using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Lumine.AuthPortal.Views.Pages;

public partial class DashboardPageView : UserControl
{
    public DashboardPageView()
    {
        InitializeComponent();
    }

    private void OnOperationalAttentionClick(object? sender, RoutedEventArgs e)
    {
        OperationalSignalsPanel.BringIntoView();
        e.Handled = true;
    }
}
