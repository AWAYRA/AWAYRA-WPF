using System.Windows;
using Awayra.App.Services;
using Awayra.App.ViewModels;

namespace Awayra.App.Views;

public partial class MainWindow : Window
{
    private AboutWindow? _aboutWindow;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Icon = AppIconHelper.ApplicationImageSource;
        AppIconHelper.ApplyToWindow(this);
    }

    private void DashboardAboutSupport_Click(object sender, RoutedEventArgs e)
    {
        if (_aboutWindow is { IsVisible: true })
        {
            _aboutWindow.Activate();
            return;
        }

        AboutWindow? window = null;
        window = new AboutWindow(new AboutViewModel(new ExternalLinkLauncher(), () => window?.Close()))
        {
            Owner = this
        };
        _aboutWindow = window;
        window.Closed += (_, _) => _aboutWindow = null;
        window.Show();
        window.Activate();
    }
}
