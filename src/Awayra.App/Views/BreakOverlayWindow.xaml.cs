using System.Windows;
using System.Windows.Automation;
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
    private readonly IMonitorSnapshotService _snapshotService;
    private Storyboard? _pulseStoryboard;
    private bool _isClosed;

    public BreakOverlayWindow(
        ApplicationHost host,
        OverlayViewModel viewModel,
        IMonitorSnapshotService snapshotService)
    {
        _host = host;
        _viewModel = viewModel;
        _snapshotService = snapshotService;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SkipCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnSkip, () => _viewModel.ShowSkip);
        viewModel.SnoozeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnSnooze, () => _viewModel.ShowSnooze);
        viewModel.CompleteCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnComplete);
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public bool HasSnapshot => _viewModel.SnapshotSource is not null;

    public void Configure(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization, bool isEye)
    {
        var prefix = isEye ? "Eye" : "Move";
        AutomationProperties.SetAutomationId(this, $"{prefix}OverlayWindow");
        AutomationProperties.SetAutomationId(OverlayCountdown, $"{prefix}OverlayCountdown");
        AutomationProperties.SetAutomationId(SkipButton, $"{prefix}SkipButton");
        AutomationProperties.SetAutomationId(SnoozeButton, $"{prefix}SnoozeButton");
        AutomationProperties.SetAutomationId(CompleteButton, $"{prefix}CompleteButton");

        var snapshot = _snapshotService.CaptureMonitorAtCursor();
        if (isEye)
        {
            _viewModel.ConfigureEye(args, settings, localization, snapshot);
        }
        else
        {
            _viewModel.ConfigureMove(args, settings, localization, snapshot);
        }
    }

    public void ShowOnActiveMonitor()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException("A closed break overlay cannot be shown again.");
        }

        MonitorLocator.PositionWindowOnCursorMonitor(this);
        if (!IsVisible)
        {
            Show();
        }

        MonitorLocator.PositionWindowOnCursorMonitor(this);
        RestoreForegroundState();
    }

    public bool TryRecoverOnActiveMonitor()
    {
        if (_isClosed)
        {
            return false;
        }

        try
        {
            WindowState = WindowState.Normal;
            if (!IsVisible)
            {
                Show();
            }

            MonitorLocator.PositionWindowOnCursorMonitor(this);
            RefreshSnapshot();
            RestoreForegroundState();
            InvalidateMeasure();
            InvalidateVisual();
            UpdateLayout();
            return IsVisible;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void UpdateRemaining(TimeSpan? remaining, LocalizationService? localization = null, int activityIndex = 0)
    {
        _viewModel.UpdateRemaining(remaining);
        if (localization is not null && !string.IsNullOrEmpty(_viewModel.InstructionPrimary))
        {
            _viewModel.InstructionPrimary = localization.GetMoveActivity(activityIndex);
        }
    }

    public void ApplyGlassClarity(int glassClarity) =>
        _viewModel.ApplyGlassClarity(glassClarity);

    public void CloseSafely()
    {
        if (_isClosed)
        {
            return;
        }

        _pulseStoryboard?.Stop();
        Close();
    }

    private void RefreshSnapshot() =>
        _viewModel.SnapshotSource = _snapshotService.CaptureMonitorAtCursor();

    private void RestoreForegroundState()
    {
        Topmost = true;
        Activate();
        Focus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
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

    private void OnClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _pulseStoryboard?.Stop();
        Loaded -= OnLoaded;
        Closed -= OnClosed;
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
