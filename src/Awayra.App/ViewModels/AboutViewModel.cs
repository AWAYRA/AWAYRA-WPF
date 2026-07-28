using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Awayra.App.Services;

namespace Awayra.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    private readonly IExternalLinkLauncher _linkLauncher;
    private readonly Action _close;

    public AboutViewModel(IExternalLinkLauncher linkLauncher, Action close)
    {
        _linkLauncher = linkLauncher;
        _close = close;
        VersionText = AppVersionInfo.GetDisplayVersion();
    }

    public string VersionText { get; }

    public string Tagline => "Your work matters. Your health matters more.";

    public string Mission =>
        "Awayra was created with love for people who spend long hours at a computer.\n\n" +
        "Technology should help us live better—not quietly take away our health.\n\n" +
        "Rest your eyes. Move your body. Keep creating.";

    public string Creator => "Created with care by Farzin Alavi.";

    public string OpenSourceStatement => "Awayra is free and open-source software.";

    public string SupportDescription =>
        "If Awayra helps you, you can optionally support its continued development.\n\n" +
        "Support is completely optional and never unlocks features.";

    public bool IsSupportConfigured => AppLinkUrls.IsSupportConfigured;

    public bool ShowSupportUnavailable => !IsSupportConfigured;

    public string SupportUnavailableMessage => "Support link is not configured yet.";

    [ObservableProperty] private bool _hasLinkError;
    [ObservableProperty] private string? _linkErrorMessage;
    [ObservableProperty] private string? _failedUrl;

    [RelayCommand]
    private void OpenSource() => LaunchLink(AppLinkUrls.Source);

    [RelayCommand]
    private void OpenIssues() => LaunchLink(AppLinkUrls.Issues);

    [RelayCommand(CanExecute = nameof(CanOpenSupport))]
    private void OpenSupport()
    {
        if (!CanOpenSupport())
        {
            return;
        }

        LaunchLink(AppLinkUrls.Support);
    }

    private bool CanOpenSupport() => IsSupportConfigured;

    [RelayCommand]
    private void CloseAbout() => _close();

    [RelayCommand]
    private void CopyFailedUrl()
    {
        if (!string.IsNullOrWhiteSpace(FailedUrl))
        {
            System.Windows.Clipboard.SetText(FailedUrl);
        }
    }

    internal void LaunchLink(string url)
    {
        HasLinkError = false;
        LinkErrorMessage = null;
        FailedUrl = null;

        var result = _linkLauncher.TryLaunch(url);
        if (result.Success)
        {
            return;
        }

        FailedUrl = result.Url ?? url;
        LinkErrorMessage = result.ErrorMessage ?? "Unable to open the link in your browser.";
        HasLinkError = true;
    }
}
