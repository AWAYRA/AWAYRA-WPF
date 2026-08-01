using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
        viewModel.ToggleSoundCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnToggleSound);
        _host.BreakSound.StateChanged += OnSoundStateChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public void Configure(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization, bool isEye)
    {
        var prefix = isEye ? "Eye" : "Move";
        AutomationProperties.SetAutomationId(this, $"{prefix}OverlayWindow");
        AutomationProperties.SetAutomationId(OverlayCountdown, $"{prefix}OverlayCountdown");
        AutomationProperties.SetAutomationId(SoundToggleButton, $"{prefix}SoundToggleButton");
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

        UpdateSoundState();
    }

    public void ShowOnActiveMonitor()
    {
        MonitorLocator.PositionWindowOnCursorMonitor(this);
        Show();
        MonitorLocator.PositionWindowOnCursorMonitor(this);
        Activate();
        Focus();
    }

    public void RepositionOnActiveMonitor()
    {
        if (IsVisible)
        {
            MonitorLocator.PositionWindowOnCursorMonitor(this);
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
        _pulseStoryboard?.Stop();
        Close();
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

    private void OnClosed(object? sender, EventArgs e) =>
        _host.BreakSound.StateChanged -= OnSoundStateChanged;

    private void OnSoundStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            UpdateSoundState();
        }
        else
        {
            Dispatcher.BeginInvoke(UpdateSoundState, DispatcherPriority.Background);
        }
    }

    private void UpdateSoundState() =>
        _viewModel.SetSoundMuted(_host.BreakSound.IsMuted);

    private void Window_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.ShowSkip)
        {
            OnSkip();
        }
    }

    private void OnToggleSound()
    {
        _host.BreakSound.ToggleMute();
        UpdateSoundState();
    }

    private void OnSkip() => _host.Scheduler.SkipActiveBreak();
    private void OnSnooze() => _host.Scheduler.SnoozeActiveBreak();
    private void OnComplete() => _host.Scheduler.CompleteActiveBreak();
}
