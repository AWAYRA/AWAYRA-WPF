using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Awayra.App.Interop;
using Awayra.App.Services;
using Awayra.App.ViewModels;
using Awayra.Core.Models;

namespace Awayra.App.Views;

public partial class BreakOverlayWindow : Window
{
    private const int RenderFramesRequiredAfterPosition = 2;

    /// <summary>
    /// How long the overlay may stay invisible while it waits for the render frames that confirm its
    /// final position. If those frames never arrive the window would sit at zero opacity for the
    /// whole break: the user sees nothing while the scheduler counts the break as delivered. Showing
    /// a possibly imperfect overlay beats showing none.
    /// </summary>
    private static readonly TimeSpan RevealTimeout = TimeSpan.FromMilliseconds(1_500);

    private readonly ApplicationHost _host;
    private readonly OverlayViewModel _viewModel;
    private readonly IMonitorSnapshotService _snapshotService;
    private readonly DispatcherTimer _monitorRecoveryTimer;
    private readonly DispatcherTimer _revealFallbackTimer;
    private readonly DisplayBoundsStabilizer _displayBoundsStabilizer = new();
    private Storyboard? _pulseStoryboard;
    private System.Drawing.Rectangle? _pendingRevealBounds;
    private int _renderFramesUntilReveal;
    private bool _waitingForRevealRender;
    private bool _firstFrameRevealed;
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
        viewModel.ToggleSoundCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OnToggleSound);
        _monitorRecoveryTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _monitorRecoveryTimer.Tick += OnMonitorRecoveryTick;
        _revealFallbackTimer = new DispatcherTimer(DispatcherPriority.Send, Dispatcher)
        {
            Interval = RevealTimeout
        };
        _revealFallbackTimer.Tick += OnRevealTimedOut;
        _host.BreakSound.StateChanged += OnSoundStateChanged;
        Loaded += OnLoaded;
        ContentRendered += OnFirstContentRendered;
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

        ConfigureExerciseView(isEye, settings.BreakAnimationEnabled);
        UpdateSoundState();
    }

    /// <summary>
    /// Shows the guided exercise that matches this break. Turning the exercise off hides the
    /// illustration entirely and leaves a plain countdown; Reduced motion keeps the illustration
    /// but replaces the movement with static guidance. Motion only starts once the overlay loads.
    /// </summary>
    private void ConfigureExerciseView(bool isEye, bool animationEnabled)
    {
        if (!animationEnabled)
        {
            EyeExercise.Visibility = Visibility.Collapsed;
            MoveExercise.Visibility = Visibility.Collapsed;
            return;
        }

        EyeExercise.Visibility = isEye ? Visibility.Visible : Visibility.Collapsed;
        MoveExercise.Visibility = isEye ? Visibility.Collapsed : Visibility.Visible;

        if (!_viewModel.ReducedMotion)
        {
            return;
        }

        if (isEye)
        {
            EyeExercise.ApplyReducedMotion();
        }
        else
        {
            MoveExercise.ApplyReducedMotion();
        }
    }

    private void StartExerciseAnimation()
    {
        if (EyeExercise.Visibility == Visibility.Visible)
        {
            EyeExercise.StartAnimation();
        }

        if (MoveExercise.Visibility == Visibility.Visible)
        {
            MoveExercise.StartAnimation();
        }
    }

    private void StopExerciseAnimation()
    {
        EyeExercise.StopAnimation();
        MoveExercise.StopAnimation();
    }

    public void ShowOnActiveMonitor()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException("A closed break overlay cannot be shown again.");
        }

        _monitorRecoveryTimer.Stop();
        _displayBoundsStabilizer.Reset();
        StopWaitingForRevealRender();

        var targetBounds = MonitorLocator.GetCursorScreen().Bounds;
        _pendingRevealBounds = targetBounds;
        _firstFrameRevealed = false;

        // A fullscreen WPF window can briefly expose its black background before the
        // snapshot and controls have completed their first render. Keep the native
        // window fully transparent and non-activating until a complete frame has been
        // rendered at the final physical monitor bounds.
        Opacity = 0;
        ShowActivated = false;
        _ = new WindowInteropHelper(this).EnsureHandle();
        _ = MonitorLocator.PositionWindowOnBounds(this, targetBounds);
        _host.Logger.Info($"Overlay prepared invisibly before first render: {targetBounds}.");
        Show();
        _revealFallbackTimer.Start();
    }

    public void RepositionOnActiveMonitor()
    {
        if (_isClosed || !IsVisible)
        {
            return;
        }

        if (!_firstFrameRevealed)
        {
            _pendingRevealBounds = MonitorLocator.GetCursorScreen().Bounds;
            _renderFramesUntilReveal = RenderFramesRequiredAfterPosition;
            return;
        }

        _monitorRecoveryTimer.Stop();
        _displayBoundsStabilizer.Reset();
        _monitorRecoveryTimer.Start();
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

        _monitorRecoveryTimer.Stop();
        _revealFallbackTimer.Stop();
        StopWaitingForRevealRender();
        _pulseStoryboard?.Stop();
        StopExerciseAnimation();
        Close();
    }

    private void OnFirstContentRendered(object? sender, EventArgs e)
    {
        if (_isClosed || !IsVisible || _firstFrameRevealed)
        {
            return;
        }

        ContentRendered -= OnFirstContentRendered;
        ApplyPendingRevealBounds();
        BeginWaitingForRevealRender();
    }

    private void ApplyPendingRevealBounds()
    {
        var targetBounds = _pendingRevealBounds ?? MonitorLocator.GetCursorScreen().Bounds;
        _pendingRevealBounds = null;

        // Any final DPI/topology correction can invalidate the frame that triggered
        // ContentRendered. Keep opacity at zero and require subsequent render frames
        // after this position has been applied before exposing the window to DWM.
        var changed = MonitorLocator.PositionWindowOnBounds(this, targetBounds);
        UpdateLayout();
        _renderFramesUntilReveal = RenderFramesRequiredAfterPosition;
        _host.Logger.Info(changed
            ? $"Overlay final bounds changed while invisible; waiting for post-position render: {targetBounds}."
            : $"Overlay final bounds confirmed while invisible; waiting for reveal render: {targetBounds}.");
    }

    private void BeginWaitingForRevealRender()
    {
        if (_waitingForRevealRender)
        {
            return;
        }

        _waitingForRevealRender = true;
        CompositionTarget.Rendering += OnPostPositionRendering;
    }

    private void StopWaitingForRevealRender()
    {
        if (!_waitingForRevealRender)
        {
            return;
        }

        CompositionTarget.Rendering -= OnPostPositionRendering;
        _waitingForRevealRender = false;
        _renderFramesUntilReveal = 0;
    }

    private void OnPostPositionRendering(object? sender, EventArgs e)
    {
        if (_isClosed || !IsVisible || _firstFrameRevealed)
        {
            StopWaitingForRevealRender();
            return;
        }

        if (_pendingRevealBounds is not null)
        {
            ApplyPendingRevealBounds();
            return;
        }

        if (_renderFramesUntilReveal > 0)
        {
            _renderFramesUntilReveal--;
            return;
        }

        StopWaitingForRevealRender();
        RevealPreparedOverlay();
    }

    private void OnRevealTimedOut(object? sender, EventArgs e)
    {
        _revealFallbackTimer.Stop();
        if (_isClosed || !IsVisible || _firstFrameRevealed)
        {
            return;
        }

        _host.Logger.Warning(
            $"Overlay reveal render did not arrive within {RevealTimeout.TotalMilliseconds:F0} ms; showing it anyway.");

        if (_pendingRevealBounds is not null)
        {
            ApplyPendingRevealBounds();
        }

        StopWaitingForRevealRender();
        RevealPreparedOverlay();
    }

    private void RevealPreparedOverlay()
    {
        if (_isClosed || !IsVisible || _firstFrameRevealed)
        {
            return;
        }

        _revealFallbackTimer.Stop();
        _firstFrameRevealed = true;
        Opacity = 1;
        ShowActivated = true;
        Activate();
        Focus();

        var bounds = MonitorLocator.GetCursorScreen().Bounds;
        _host.Logger.Info($"Overlay revealed after complete post-position render: {bounds}.");
    }

    private void OnMonitorRecoveryTick(object? sender, EventArgs e)
    {
        if (_isClosed || !IsVisible || !_firstFrameRevealed)
        {
            _monitorRecoveryTimer.Stop();
            _displayBoundsStabilizer.Reset();
            return;
        }

        var observedBounds = MonitorLocator.GetCursorScreen().Bounds;
        if (!_displayBoundsStabilizer.Observe(observedBounds))
        {
            return;
        }

        _monitorRecoveryTimer.Stop();
        var stableBounds = _displayBoundsStabilizer.CurrentBounds ?? observedBounds;
        _displayBoundsStabilizer.Reset();

        if (MonitorLocator.IsWindowAtPhysicalBounds(this, stableBounds))
        {
            _host.Logger.Info($"Overlay display recovery skipped; physical bounds unchanged: {stableBounds}.");
            return;
        }

        if (MonitorLocator.PositionWindowOnBounds(this, stableBounds))
        {
            _host.Logger.Info($"Overlay repositioned once after display stabilization: {stableBounds}.");
        }
        else
        {
            _host.Logger.Warning($"Overlay display recovery could not apply stable bounds: {stableBounds}.");
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.ReducedMotion)
        {
            StartExerciseAnimation();

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
        _firstFrameRevealed = false;
        _pendingRevealBounds = null;
        _monitorRecoveryTimer.Stop();
        _monitorRecoveryTimer.Tick -= OnMonitorRecoveryTick;
        _revealFallbackTimer.Stop();
        _revealFallbackTimer.Tick -= OnRevealTimedOut;
        StopWaitingForRevealRender();
        _displayBoundsStabilizer.Reset();
        _pulseStoryboard?.Stop();
        StopExerciseAnimation();
        _host.BreakSound.StateChanged -= OnSoundStateChanged;
        Loaded -= OnLoaded;
        ContentRendered -= OnFirstContentRendered;
        Closed -= OnClosed;
    }

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
