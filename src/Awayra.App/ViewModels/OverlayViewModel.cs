using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Awayra.App.Services;
using Awayra.Core.Localization;
using Awayra.Core.Models;
using Awayra.Core.Services;
using System.Globalization;
using System.Windows.Media;

namespace Awayra.App.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _instructionPrimary = string.Empty;
    [ObservableProperty] private string _instructionSecondary = string.Empty;
    [ObservableProperty] private string _remainingText = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _showSkip;
    [ObservableProperty] private bool _showSnooze;
    [ObservableProperty] private bool _reducedMotion;
    [ObservableProperty] private int _glassClarity = OverlayGlassSettings.DefaultGlassClarity;
    [ObservableProperty] private ImageSource? _snapshotSource;
    [ObservableProperty] private double _blurRadius = OverlayGlassSettings.BlurRadiusFromClarity(OverlayGlassSettings.DefaultGlassClarity);
    [ObservableProperty] private bool _isSoundMuted = true;
    [ObservableProperty] private string _soundToggleText = string.Empty;
    [ObservableProperty] private string _skipText = string.Empty;
    [ObservableProperty] private string _snoozeText = string.Empty;
    [ObservableProperty] private string _completeText = string.Empty;

    /// <summary>
    /// False while a break with skipping disabled is still running. Complete ends the break early and
    /// records it as completed, so leaving it live gave a way around a disabled Skip that also
    /// inflated the daily statistics.
    /// </summary>
    [ObservableProperty] private bool _canComplete = true;

    private LocalizationService? _localization;
    private bool _allowSkip = true;

    public double BackgroundTintOpacity => OverlayGlassSettings.BackgroundTintOpacityFromClarity(GlassClarity);

    public double ContentOpacity => OverlayGlassSettings.ContentOpacity;

    public IRelayCommand? SkipCommand { get; set; }
    public IRelayCommand? SnoozeCommand { get; set; }
    public IRelayCommand? CompleteCommand { get; set; }
    public IRelayCommand? ToggleSoundCommand { get; set; }

    public void ConfigureEye(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization, ImageSource? snapshot)
    {
        Title = localization.Get(StringKeys.EyeReset);
        InstructionPrimary = localization.Get(StringKeys.EyeResetInstructionDistance);
        InstructionSecondary = localization.Get(StringKeys.EyeResetInstructionBlink);
        ApplyCommonConfiguration(settings, localization, snapshot);
        UpdateRemaining(TimeSpan.FromSeconds(args.DurationSeconds));
    }

    public void ConfigureMove(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization, ImageSource? snapshot)
    {
        Title = localization.Get(StringKeys.MoveBreak);
        InstructionPrimary = localization.GetMoveActivity(args.ActivityIndex);
        InstructionSecondary = string.Empty;
        ApplyCommonConfiguration(settings, localization, snapshot);
        UpdateRemaining(TimeSpan.FromSeconds(args.DurationSeconds));
    }

    private void ApplyCommonConfiguration(AppSettings settings, LocalizationService localization, ImageSource? snapshot)
    {
        _localization = localization;
        _allowSkip = settings.AllowSkip;
        ShowSkip = settings.AllowSkip;
        ShowSnooze = settings.AllowSnooze;
        ReducedMotion = settings.ReducedMotion;
        SkipText = localization.Get(StringKeys.Skip);
        SnoozeText = localization.Get(StringKeys.Snooze);
        CompleteText = localization.Get(StringKeys.Complete);
        CanComplete = settings.AllowSkip;
        ApplyGlassClarity(settings.GlassClarity);
        SnapshotSource = snapshot;
        SetSoundMuted(IsSoundMuted);
    }

    public void SetSoundMuted(bool muted)
    {
        IsSoundMuted = muted;
        SoundToggleText = muted
            ? _localization?.Get(StringKeys.SoundMuted) ?? "Muted"
            : _localization?.Get(StringKeys.SoundOn) ?? "Sound on";
    }

    public void ApplyGlassClarity(int glassClarity)
    {
        GlassClarity = OverlayGlassSettings.NormalizeGlassClarity(glassClarity);
        BlurRadius = OverlayGlassSettings.BlurRadiusFromClarity(GlassClarity);
        OnPropertyChanged(nameof(BackgroundTintOpacity));
    }

    public void UpdateRemaining(TimeSpan? remaining)
    {
        if (remaining is null)
        {
            return;
        }

        var seconds = Math.Max(0, (int)remaining.Value.TotalSeconds);
        RemainingText = seconds.ToString(CultureInfo.CurrentCulture);
        Progress = seconds;

        if (!_allowSkip)
        {
            CanComplete = seconds <= 0;
        }
    }
}
