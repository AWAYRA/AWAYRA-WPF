using System.Windows;
using Awayra.App.ViewModels;

namespace Awayra.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
