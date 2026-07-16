using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Awayra.App.Services;
using Awayra.Core.Localization;
using Awayra.Core.Models;

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
    [ObservableProperty] private double _overlayOpacity = 0.82;
    [ObservableProperty] private System.Windows.FlowDirection _flowDirection;

    public IRelayCommand? SkipCommand { get; set; }
    public IRelayCommand? SnoozeCommand { get; set; }
    public IRelayCommand? CompleteCommand { get; set; }

    public void ConfigureEye(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization)
    {
        Title = localization.Get(StringKeys.EyeReset);
        InstructionPrimary = localization.Get(StringKeys.EyeResetInstructionDistance);
        InstructionSecondary = localization.Get(StringKeys.EyeResetInstructionBlink);
        ShowSkip = settings.AllowSkip;
        ShowSnooze = settings.AllowSnooze;
        ReducedMotion = settings.ReducedMotion;
        OverlayOpacity = settings.OverlayOpacity;
        FlowDirection = localization.CurrentFlowDirection;
        UpdateRemaining(TimeSpan.FromSeconds(args.DurationSeconds));
    }

    public void ConfigureMove(BreakStartedEventArgs args, AppSettings settings, LocalizationService localization)
    {
        Title = localization.Get(StringKeys.MoveBreak);
        InstructionPrimary = localization.GetMoveActivity(args.ActivityIndex);
        InstructionSecondary = string.Empty;
        ShowSkip = settings.AllowSkip;
        ShowSnooze = settings.AllowSnooze;
        ReducedMotion = settings.ReducedMotion;
        OverlayOpacity = settings.OverlayOpacity;
        FlowDirection = localization.CurrentFlowDirection;
        UpdateRemaining(TimeSpan.FromSeconds(args.DurationSeconds));
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
