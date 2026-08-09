namespace Awayra.Core.Services;

public static class OverlayGlassSettings
{
    public const int MinGlassClarity = 0;
    public const int MaxGlassClarity = 150;
    public const int DefaultGlassClarity = 100;
    public const double MinBlurRadius = 8.0;
    public const double MaxBlurRadius = 30.0;

    public static int NormalizeGlassClarity(int value) =>
        Math.Clamp(value, MinGlassClarity, MaxGlassClarity);

    public static int NormalizeGlassClarity(double value) =>
        NormalizeGlassClarity((int)Math.Round(value));

    public static double BackgroundTintOpacityFromClarity(int glassClarity)
    {
        var normalized = NormalizeGlassClarity(glassClarity);
        if (normalized <= 100)
        {
            return 1.0 - normalized / 100.0;
        }

        return 0.0;
    }

    public static double BlurRadiusFromClarity(int glassClarity)
    {
        var normalized = NormalizeGlassClarity(glassClarity);
        double radius;
        if (normalized <= 100)
        {
            radius = 30.0 - normalized * 0.12;
        }
        else
        {
            radius = 18.0 - ((normalized - 100.0) / 50.0) * 10.0;
        }

        return Math.Clamp(radius, MinBlurRadius, MaxBlurRadius);
    }

    public static int MigrateFromGlassTransparency(int glassTransparency) =>
        NormalizeGlassClarity(glassTransparency);

    public static int MigrateFromBackgroundVisibility(int backgroundVisibility)
    {
        if (backgroundVisibility <= 10)
        {
            return 0;
        }

        if (backgroundVisibility >= 30)
        {
            return 100;
        }

        if (backgroundVisibility <= 20)
        {
            return (int)Math.Round((backgroundVisibility - 10) * 5.0);
        }

        return (int)Math.Round(50 + (backgroundVisibility - 20) * 5.0);
    }

    public static int MigrateFromLegacyOpacity(double legacyOpacity)
    {
        if (double.IsNaN(legacyOpacity) || double.IsInfinity(legacyOpacity))
        {
            return DefaultGlassClarity;
        }

        var visibility = (int)Math.Round((1.0 - legacyOpacity) * 100.0);
        return MigrateFromBackgroundVisibility(Math.Clamp(visibility, 10, 30));
    }

    public static double ContentOpacity => 1.0;
}
