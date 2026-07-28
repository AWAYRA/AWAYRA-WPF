using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Awayra.App.Services;
using Awayra.Core.Localization;
using Awayra.Core.Models;
using Awayra.Core.Services;
using System.Windows.Media;
using System.Windows.Media.Effects;

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

    public double BackgroundTintOpacity => OverlayGlassSettings.BackgroundTintOpacityFromClarity(GlassClarity);

    public double ContentOpacity => OverlayGlassSettings.ContentOpacity;

    public IRelayCommand? SkipCommand { get; set; }
    public IRelayCommand? SnoozeCommand { get; set; }
    public IRelayCommand? CompleteCommand { get; set; }

    public void ConfigureEye(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization, ImageSource? snapshot)
    {
        Title = localization.Get(StringKeys.EyeReset);
        InstructionPrimary = localization.Get(StringKeys.EyeResetInstructionDistance);
        InstructionSecondary = localization.Get(StringKeys.EyeResetInstructionBlink);
        ShowSkip = settings.AllowSkip;
        ShowSnooze = settings.AllowSnooze;
        ReducedMotion = settings.ReducedMotion;
        ApplyGlassClarity(settings.GlassClarity);
        SnapshotSource = snapshot;
        UpdateRemaining(TimeSpan.FromSeconds(args.DurationSeconds));
    }

    public void ConfigureMove(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization, ImageSource? snapshot)
    {
        Title = localization.Get(StringKeys.MoveBreak);
        InstructionPrimary = localization.GetMoveActivity(args.ActivityIndex);
        InstructionSecondary = string.Empty;
        ShowSkip = settings.AllowSkip;
        ShowSnooze = settings.AllowSnooze;
        ReducedMotion = settings.ReducedMotion;
        ApplyGlassClarity(settings.GlassClarity);
        SnapshotSource = snapshot;
        UpdateRemaining(TimeSpan.FromSeconds(args.DurationSeconds));
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
        RemainingText = seconds.ToString();
        Progress = seconds;
    }
}
