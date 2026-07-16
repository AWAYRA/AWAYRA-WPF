using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Awayra.App.Interop;
using Awayra.App.Services;
using Awayra.App.ViewModels;
using Awayra.Core.Models;

namespace Awayra.App.Views;

public partial class BreakOverlayWindow : Window
{
    private readonly ApplicationHost _host;
    private readonly OverlayViewModel _viewModel;
    private Storyboard? _pulseStoryboard;

    public BreakOverlayWindow(ApplicationHost host, OverlayViewModel viewModel)
    {
        _host = host;
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SkipCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnSkip, () => _viewModel.ShowSkip);
        viewModel.SnoozeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnSnooze, () => _viewModel.ShowSnooze);
        viewModel.CompleteCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnComplete);
        Loaded += OnLoaded;
    }

    public void Configure(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization, bool isEye)
    {
        if (isEye)
        {
            _viewModel.ConfigureEye(args, settings, localization);
        }
        else
        {
            _viewModel.ConfigureMove(args, settings, localization);
        }
    }

    public void ShowOnActiveMonitor()
    {
        MonitorLocator.PositionWindowOnCursorMonitor(this);
        Show();
        Activate();
        Focus();
    }

    public void UpdateRemaining(TimeSpan? remaining, LocalizationService? localization = null, int activityIndex = 0)
    {
        _viewModel.UpdateRemaining(remaining);
        if (localization is not null && !string.IsNullOrEmpty(_viewModel.InstructionPrimary))
        {
            _viewModel.InstructionPrimary = localization.GetMoveActivity(activityIndex);
        }
    }

    public void CloseSafely()
    {
        _pulseStoryboard?.Stop();
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DwmHelper.TryApplyBackdrop(this, true);
        if (!_viewModel.ReducedMotion)
        {
            _pulseStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var animation = new DoubleAnimation(1, 1.15, TimeSpan.FromSeconds(2.4))
            {
                AutoReverse = true,
                EasingFunction = new SineEase()
            };
            Storyboard.SetTarget(animation, PulseRing);
            Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.ScaleX"));
            _pulseStoryboard.Children.Add(animation);

            var animationY = animation.Clone();
            Storyboard.SetTarget(animationY, PulseRing);
            Storyboard.SetTargetProperty(animationY, new PropertyPath("RenderTransform.ScaleY"));
            _pulseStoryboard.Children.Add(animationY);
            _pulseStoryboard.Begin();
        }
    }

    private void Window_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.ShowSkip)
        {
            OnSkip();
        }
    }

    private void OnSkip() => _host.Scheduler.SkipActiveBreak();
    private void OnSnooze() => _host.Scheduler.SnoozeActiveBreak();
    private void OnComplete() => _host.Scheduler.CompleteActiveBreak();
}
