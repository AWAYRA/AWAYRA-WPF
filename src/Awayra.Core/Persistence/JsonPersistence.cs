using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Awayra.Core.Abstractions;
using Awayra.Core.Models;
using Awayra.Core.Services;

namespace Awayra.Core.Persistence;

public static class JsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new TimeOnlyJsonConverter());
        return options;
    }
}

public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    // Persisted times are a wire format, not a display format. "HH:mm" resolves ":" through the
    // ambient culture's time separator, so a locale that writes 09.00 could not read its own file
    // back once the UI thread had forced the culture to English.
    private const string Format = "HH\\:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeOnly.MinValue;
        }

        return TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            || TimeOnly.TryParse(value, CultureInfo.CurrentCulture, out parsed)
                ? parsed
                : TimeOnly.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}

public sealed class InMemorySettingsStore : ISettingsStore
{
    private AppSettings _settings = AppSettings.CreateDefault();

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryStateStore : IStateStore
{
    private SchedulerState? _state;

    public Task<SchedulerState?> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_state);

    public Task SaveAsync(SchedulerState state, CancellationToken cancellationToken = default)
    {
        _state = state;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryStatisticsStore : IStatisticsStore
{
    private StatisticsData _data = StatisticsData.CreateDefault();

    public Task<StatisticsData> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_data);

    public Task SaveAsync(StatisticsData data, CancellationToken cancellationToken = default)
    {
        _data = data;
        return Task.CompletedTask;
    }
}

public sealed class SettingsRecovery
{
    public static AppSettings LoadWithRecovery(string json, IAppLogger? logger = null)
    {
        var settings = AppSettings.CreateDefault();

        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions.Create()) ?? settings;
        }
        catch (JsonException ex)
        {
            logger?.Warning($"Settings JSON corrupt: {ex.Message}");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                ApplyPartialProperties(doc.RootElement, settings);
                ApplyMigrations(doc.RootElement, settings);
            }
        }
        catch (Exception ex)
        {
            logger?.Warning($"Settings document parse failed: {ex.Message}");
        }

        return Repair(settings, logger);
    }

    private static void ApplyMigrations(JsonElement root, AppSettings settings)
    {
        var hasGlassClarity = false;
        double? legacyOpacity = null;

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "glassclarity":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var clarity))
                    {
                        settings.GlassClarity = clarity;
                        hasGlassClarity = true;
                    }

                    break;
                case "glasstransparency":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var glass))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromGlassTransparency(glass);
                        hasGlassClarity = true;
                    }

                    break;
                case "backgroundvisibility":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var visibility))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromBackgroundVisibility(visibility);
                        hasGlassClarity = true;
                    }

                    break;
                case "overlayopacity":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetDouble(out var opacity))
                    {
                        legacyOpacity = opacity;
                    }

                    break;
            }
        }

        if (!hasGlassClarity && legacyOpacity.HasValue)
        {
            settings.GlassClarity = OverlayGlassSettings.MigrateFromLegacyOpacity(legacyOpacity.Value);
        }
    }

    private static void ApplyPartialProperties(JsonElement root, AppSettings settings)
    {
        // Every branch checks ValueKind before reading. An unguarded read used to throw out of the
        // whole loop, so a single mistyped value silently skipped every later property and the
        // legacy migrations that run after it.
        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "eyeresetenabled":
                    if (TryGetBoolean(property.Value, out var eyeEnabled))
                    {
                        settings.EyeResetEnabled = eyeEnabled;
                    }

                    break;
                case "eyeresetintervalminutes":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var eyeInt))
                    {
                        settings.EyeResetIntervalMinutes = eyeInt;
                    }

                    break;
                case "movebreakenabled":
                    if (TryGetBoolean(property.Value, out var moveEnabled))
                    {
                        settings.MoveBreakEnabled = moveEnabled;
                    }

                    break;
                case "overlayopacity":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetDouble(out var legacyOpacity))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromLegacyOpacity(legacyOpacity);
                    }

                    break;
                case "backgroundvisibility":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var visibility))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromBackgroundVisibility(visibility);
                    }

                    break;
                case "glassclarity":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var clarity))
                    {
                        settings.GlassClarity = clarity;
                    }

                    break;
                case "glasstransparency":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var glass))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromGlassTransparency(glass);
                    }

                    break;
            }
        }
    }

    private static bool TryGetBoolean(JsonElement element, out bool value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.String:
                return bool.TryParse(element.GetString(), out value);
            default:
                value = false;
                return false;
        }
    }

    /// <summary>
    /// Brings every field back inside its validated range, in place. One unusable number must never
    /// cost the user the rest of their configuration: each field is clamped on its own, so a broken
    /// duration cannot take work hours, sound choice or Windows preferences down with it.
    /// </summary>
    public static AppSettings Repair(AppSettings loaded, IAppLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(loaded);

        var defaults = AppSettings.CreateDefault();
        var before = SettingsValidator.Validate(loaded);

        if (loaded.SchemaVersion <= 0)
        {
            loaded.SchemaVersion = AppSettings.CurrentSchemaVersion;
        }

        loaded.EyeResetIntervalMinutes = Math.Clamp(
            loaded.EyeResetIntervalMinutes,
            SettingsValidator.MinIntervalMinutes,
            SettingsValidator.MaxIntervalMinutes);
        loaded.MoveBreakIntervalMinutes = Math.Clamp(
            loaded.MoveBreakIntervalMinutes,
            SettingsValidator.MinIntervalMinutes,
            SettingsValidator.MaxIntervalMinutes);

        // A break can never outlast the gap it interrupts, so the upper bound is whichever of the
        // absolute maximum and the reminder's own interval is smaller.
        loaded.EyeResetDurationSeconds = ClampDuration(
            loaded.EyeResetDurationSeconds,
            loaded.EyeResetIntervalMinutes);
        loaded.MoveBreakDurationSeconds = ClampDuration(
            loaded.MoveBreakDurationSeconds,
            loaded.MoveBreakIntervalMinutes);

        loaded.SnoozeDurationMinutes = Math.Clamp(
            loaded.SnoozeDurationMinutes,
            SettingsValidator.MinSnoozeMinutes,
            SettingsValidator.MaxSnoozeMinutes);
        loaded.IdleThresholdMinutes = Math.Clamp(
            loaded.IdleThresholdMinutes,
            SettingsValidator.MinIdleMinutes,
            SettingsValidator.MaxIdleMinutes);
        loaded.BreakSoundVolume = Math.Clamp(
            loaded.BreakSoundVolume,
            SettingsValidator.MinBreakSoundVolume,
            SettingsValidator.MaxBreakSoundVolume);
        loaded.BreakSoundRepeatSeconds = Math.Clamp(
            loaded.BreakSoundRepeatSeconds,
            SettingsValidator.MinBreakSoundRepeatSeconds,
            SettingsValidator.MaxBreakSoundRepeatSeconds);
        loaded.GlassClarity = OverlayGlassSettings.NormalizeGlassClarity(loaded.GlassClarity);

        if (!Enum.IsDefined(loaded.BreakSoundTheme))
        {
            loaded.BreakSoundTheme = defaults.BreakSoundTheme;
        }

        if (loaded.WorkHoursEnabled && loaded.WorkStart == loaded.WorkEnd)
        {
            loaded.WorkStart = defaults.WorkStart;
            loaded.WorkEnd = defaults.WorkEnd;
        }

        if (before.Count > 0)
        {
            logger?.Warning(
                $"Repaired out-of-range settings instead of resetting them: {string.Join(", ", before)}");
        }

        return loaded;
    }

    private static int ClampDuration(int durationSeconds, int intervalMinutes)
    {
        // intervalMinutes is already clamped to at least one minute, so the ceiling can never fall
        // below MinDurationSeconds.
        var maximum = Math.Min(SettingsValidator.MaxDurationSeconds, intervalMinutes * 60);
        return Math.Clamp(durationSeconds, SettingsValidator.MinDurationSeconds, maximum);
    }
}

public sealed class NullLogger : IAppLogger
{
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? exception = null) { }
    public Task FlushAsync() => Task.CompletedTask;
}
