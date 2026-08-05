using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Awayra.App.Services;
using Awayra.App.ViewModels;

namespace Awayra.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Icon = AppIconHelper.ApplicationImageSource;
        AppIconHelper.ApplyToWindow(this);
        AutomationProperties.SetAutomationId(GlassClaritySlider, "GlassClarityInput");
        Loaded += (_, _) => GlassClaritySlider.BringIntoView();
    }

    /// <summary>
    /// A number WPF cannot read leaves the source property holding its previous value. The red
    /// border alone was not enough: Save went ahead with the old number and looked like it had
    /// accepted what was on screen. Track the failures so the view model can refuse the save.
    /// </summary>
    private void OnFieldValidationError(object sender, ValidationErrorEventArgs e)
    {
        if (e.Error?.BindingInError is not BindingExpression binding)
        {
            return;
        }

        var propertyName = binding.ResolvedSourcePropertyName;
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        _viewModel.SetFieldReadFailure(propertyName, e.Action == ValidationErrorEventAction.Added);
    }
}
