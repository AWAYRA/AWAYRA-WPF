using System.Windows;
using Awayra.App.Services;
using Awayra.App.ViewModels;

namespace Awayra.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Icon = AppIconHelper.ApplicationImageSource;
        AppIconHelper.ApplyToWindow(this);
    }
}
