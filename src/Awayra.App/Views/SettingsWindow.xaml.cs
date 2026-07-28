using System.Windows;
using System.Windows.Automation;
using Awayra.App.ViewModels;

namespace Awayra.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        AutomationProperties.SetAutomationId(GlassClaritySlider, "GlassClarityInput");
        Loaded += (_, _) => GlassClaritySlider.BringIntoView();
    }
}
