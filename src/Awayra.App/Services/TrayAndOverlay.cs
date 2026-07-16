using System.Drawing;
using System.Windows;
using Awayra.Core.Abstractions;
using Awayra.Core.Localization;
using Awayra.App.Views;
using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.App.Services;

public sealed class OverlayCoordinator
{
    private readonly Func<BreakOverlayWindow> _eyeOverlayFactory;
    private readonly Func<BreakOverlayWindow> _moveOverlayFactory;
    private readonly IAppLogger _logger;

    private BreakOverlayWindow? _eyeOverlay;
    private BreakOverlayWindow? _moveOverlay;

    public OverlayCoordinator(
        Func<BreakOverlayWindow> eyeOverlayFactory,
        Func<BreakOverlayWindow> moveOverlayFactory,
        IAppLogger logger)
    {
        _eyeOverlayFactory = eyeOverlayFactory;
        _moveOverlayFactory = moveOverlayFactory;
        _logger = logger;
    }

    public void ShowBreak(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization)
    {
        try
        {
            CloseAll();
            if (args.BreakType == BreakType.Eye)
            {
                _eyeOverlay = _eyeOverlayFactory();
                _eyeOverlay.Configure(args, settings, localization, isEye: true);
                _eyeOverlay.ShowOnActiveMonitor();
            }
            else
            {
                _moveOverlay = _moveOverlayFactory();
                _moveOverlay.Configure(args, settings, localization, isEye: false);
                _moveOverlay.ShowOnActiveMonitor();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to show overlay", ex);
        }
    }

    public void UpdateActiveBreak(TimeSpan? remaining, LocalizationService localization, int activityIndex)
    {
        if (_eyeOverlay is not null && _eyeOverlay.IsVisible)
        {
            _eyeOverlay.UpdateRemaining(remaining);
        }

        if (_moveOverlay is not null && _moveOverlay.IsVisible)
        {
            _moveOverlay.UpdateRemaining(remaining, localization, activityIndex);
        }
    }

    public void CloseAll()
    {
        if (_eyeOverlay is not null)
        {
            _eyeOverlay.CloseSafely();
            _eyeOverlay = null;
        }

        if (_moveOverlay is not null)
        {
            _moveOverlay.CloseSafely();
            _moveOverlay = null;
        }
    }
}

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly LocalizationService _localization;
    private readonly Action _openDashboard;
    private readonly Action _eyeNow;
    private readonly Action _moveNow;
    private readonly Action _togglePause;
    private readonly Action _openSettings;
    private readonly Action _quit;
    private readonly Func<string> _tooltipProvider;

    public TrayService(
        Icon trayIcon,
        LocalizationService localization,
        Action openDashboard,
        Action eyeNow,
        Action moveNow,
        Action togglePause,
        Action openSettings,
        Action quit,
        Func<string> tooltipProvider)
    {
        _localization = localization;
        _openDashboard = openDashboard;
        _eyeNow = eyeNow;
        _moveNow = moveNow;
        _togglePause = togglePause;
        _openSettings = openSettings;
        _quit = quit;
        _tooltipProvider = tooltipProvider;

        _menu = new ContextMenuStrip();
        _icon = new NotifyIcon
        {
            Icon = trayIcon,
            Visible = true,
            Text = "Awayra"
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _openDashboard();
            }
        };

        _icon.DoubleClick += (_, _) => _openDashboard();
        RebuildMenu();
        _localization.CultureChanged += (_, _) => RebuildMenu();
    }

    public void UpdateTooltip() => _icon.Text = TrimTooltip(_tooltipProvider());

    private void RebuildMenu()
    {
        _menu.Items.Clear();
        _menu.Items.Add(_localization.Get(StringKeys.OpenAwayra), null, (_, _) => _openDashboard());
        _menu.Items.Add(_localization.Get(StringKeys.EyeResetNow), null, (_, _) => _eyeNow());
        _menu.Items.Add(_localization.Get(StringKeys.MoveBreakNow), null, (_, _) => _moveNow());
        _menu.Items.Add(_localization.Get(StringKeys.TrayPauseReminders), null, (_, _) => _togglePause());
        _menu.Items.Add(_localization.Get(StringKeys.Settings), null, (_, _) => _openSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_localization.Get(StringKeys.Quit), null, (_, _) => _quit());
        _icon.ContextMenuStrip = _menu;
    }

    public void SetPauseMenuLabel(bool paused)
    {
        if (_menu.Items.Count > 3)
        {
            _menu.Items[3].Text = paused
                ? _localization.Get(StringKeys.TrayResumeReminders)
                : _localization.Get(StringKeys.TrayPauseReminders);
        }
    }

    private static string TrimTooltip(string text) =>
        text.Length <= 63 ? text : text[..60] + "...";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
