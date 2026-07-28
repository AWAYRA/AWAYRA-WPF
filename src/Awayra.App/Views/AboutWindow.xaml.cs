using System.Windows;
using Awayra.App.ViewModels;

namespace Awayra.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
