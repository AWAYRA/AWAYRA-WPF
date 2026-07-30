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
    private static readonly string[] SupportedFormats = ["H:mm", "HH:mm"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Time values must be JSON strings in HH:mm format.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value) ||
            !TimeOnly.TryParseExact(value, SupportedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new JsonException($"Invalid time value '{value}'. Expected HH:mm.");
        }

        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm", CultureInfo.InvariantCulture));
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
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm"];

    public static void ApplyDocumentProperties(JsonElement root, AppSettings settings) =>
        ApplyPartialProperties(root, settings);

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
            ApplyPartialProperties(doc.RootElement, settings);
            ApplyMigrations(doc.RootElement, settings);
        }
        catch (Exception ex)
        {
            logger?.Warning($"Settings document parse failed: {ex.Message}");
        }

        return Normalize(settings);
    }

    public static AppSettings Normalize(AppSettings loaded)
    {
        var defaults = AppSettings.CreateDefault();

        if (loaded.SchemaVersion <= 0)
        {
            loaded.SchemaVersion = AppSettings.CurrentSchemaVersion;
        }

        if (loaded.EyeResetIntervalMinutes < SettingsValidator.MinIntervalMinutes ||
            loaded.EyeResetIntervalMinutes > SettingsValidator.MaxIntervalMinutes)
        {
            loaded.EyeResetIntervalMinutes = defaults.EyeResetIntervalMinutes;
        }

        if (loaded.EyeResetDurationSeconds < SettingsValidator.MinDurationSeconds ||
            loaded.EyeResetDurationSeconds > SettingsValidator.MaxDurationSeconds ||
            loaded.EyeResetDurationSeconds > loaded.EyeResetIntervalMinutes * 60)
        {
            loaded.EyeResetDurationSeconds = defaults.EyeResetDurationSeconds;
        }

        if (loaded.MoveBreakIntervalMinutes < SettingsValidator.MinIntervalMinutes ||
            loaded.MoveBreakIntervalMinutes > SettingsValidator.MaxIntervalMinutes)
        {
            loaded.MoveBreakIntervalMinutes = defaults.MoveBreakIntervalMinutes;
        }

        if (loaded.MoveBreakDurationSeconds < SettingsValidator.MinDurationSeconds ||
            loaded.MoveBreakDurationSeconds > SettingsValidator.MaxDurationSeconds ||
            loaded.MoveBreakDurationSeconds > loaded.MoveBreakIntervalMinutes * 60)
        {
            loaded.MoveBreakDurationSeconds = defaults.MoveBreakDurationSeconds;
        }

        if (loaded.SnoozeDurationMinutes < SettingsValidator.MinSnoozeMinutes ||
            loaded.SnoozeDurationMinutes > SettingsValidator.MaxSnoozeMinutes)
        {
            loaded.SnoozeDurationMinutes = defaults.SnoozeDurationMinutes;
        }

        if (loaded.IdleThresholdMinutes < SettingsValidator.MinIdleMinutes ||
            loaded.IdleThresholdMinutes > SettingsValidator.MaxIdleMinutes)
        {
            loaded.IdleThresholdMinutes = defaults.IdleThresholdMinutes;
        }

        if (loaded.GlassClarity < OverlayGlassSettings.MinGlassClarity ||
            loaded.GlassClarity > OverlayGlassSettings.MaxGlassClarity)
        {
            loaded.GlassClarity = defaults.GlassClarity;
        }
        else
        {
            loaded.GlassClarity = OverlayGlassSettings.NormalizeGlassClarity(loaded.GlassClarity);
        }

        if (loaded.WorkHoursEnabled && loaded.WorkStart == loaded.WorkEnd)
        {
            loaded.WorkStart = defaults.WorkStart;
            loaded.WorkEnd = defaults.WorkEnd;
        }

        if (!Enum.IsDefined(loaded.Theme))
        {
            loaded.Theme = defaults.Theme;
        }

        return loaded;
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
                    if (TryGetInt(property.Value, out var clarity))
                    {
                        settings.GlassClarity = clarity;
                        hasGlassClarity = true;
                    }
                    break;
                case "glasstransparency":
                    if (TryGetInt(property.Value, out var glass))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromGlassTransparency(glass);
                        hasGlassClarity = true;
                    }
                    break;
                case "backgroundvisibility":
                    if (TryGetInt(property.Value, out var visibility))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromBackgroundVisibility(visibility);
                        hasGlassClarity = true;
                    }
                    break;
                case "overlayopacity":
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var opacity))
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
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "schemaversion":
                    if (TryGetInt(property.Value, out var schemaVersion)) settings.SchemaVersion = schemaVersion;
                    break;
                case "eyeresetenabled":
                    if (TryGetBoolean(property.Value, out var eyeEnabled)) settings.EyeResetEnabled = eyeEnabled;
                    break;
                case "eyeresetintervalminutes":
                    if (TryGetInt(property.Value, out var eyeInterval)) settings.EyeResetIntervalMinutes = eyeInterval;
                    break;
                case "eyeresetdurationseconds":
                    if (TryGetInt(property.Value, out var eyeDuration)) settings.EyeResetDurationSeconds = eyeDuration;
                    break;
                case "movebreakenabled":
                    if (TryGetBoolean(property.Value, out var moveEnabled)) settings.MoveBreakEnabled = moveEnabled;
                    break;
                case "movebreakintervalminutes":
                    if (TryGetInt(property.Value, out var moveInterval)) settings.MoveBreakIntervalMinutes = moveInterval;
                    break;
                case "movebreakdurationseconds":
                    if (TryGetInt(property.Value, out var moveDuration)) settings.MoveBreakDurationSeconds = moveDuration;
                    break;
                case "allowskip":
                    if (TryGetBoolean(property.Value, out var allowSkip)) settings.AllowSkip = allowSkip;
                    break;
                case "allowsnooze":
                    if (TryGetBoolean(property.Value, out var allowSnooze)) settings.AllowSnooze = allowSnooze;
                    break;
                case "snoozedurationminutes":
                    if (TryGetInt(property.Value, out var snooze)) settings.SnoozeDurationMinutes = snooze;
                    break;
                case "pausewhileidle":
                    if (TryGetBoolean(property.Value, out var pauseIdle)) settings.PauseWhileIdle = pauseIdle;
                    break;
                case "idlethresholdminutes":
                    if (TryGetInt(property.Value, out var idleThreshold)) settings.IdleThresholdMinutes = idleThreshold;
                    break;
                case "workhoursenabled":
                    if (TryGetBoolean(property.Value, out var workEnabled)) settings.WorkHoursEnabled = workEnabled;
                    break;
                case "workstart":
                    if (TryGetTime(property.Value, out var workStart)) settings.WorkStart = workStart;
                    break;
                case "workend":
                    if (TryGetTime(property.Value, out var workEnd)) settings.WorkEnd = workEnd;
                    break;
                case "runatstartup":
                    if (TryGetBoolean(property.Value, out var runAtStartup)) settings.RunAtStartup = runAtStartup;
                    break;
                case "startminimized":
                    if (TryGetBoolean(property.Value, out var startMinimized)) settings.StartMinimized = startMinimized;
                    break;
                case "closetotray":
                    if (TryGetBoolean(property.Value, out var closeToTray)) settings.CloseToTray = closeToTray;
                    break;
                case "glassclarity":
                    if (TryGetInt(property.Value, out var clarity)) settings.GlassClarity = clarity;
                    break;
                case "glasstransparency":
                    if (TryGetInt(property.Value, out var glass)) settings.GlassClarity = OverlayGlassSettings.MigrateFromGlassTransparency(glass);
                    break;
                case "backgroundvisibility":
                    if (TryGetInt(property.Value, out var visibility)) settings.GlassClarity = OverlayGlassSettings.MigrateFromBackgroundVisibility(visibility);
                    break;
                case "overlayopacity":
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var opacity))
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromLegacyOpacity(opacity);
                    }
                    break;
                case "reducedmotion":
                    if (TryGetBoolean(property.Value, out var reducedMotion)) settings.ReducedMotion = reducedMotion;
                    break;
                case "theme":
                    if (TryGetTheme(property.Value, out var theme)) settings.Theme = theme;
                    break;
            }
        }
    }

    private static bool TryGetBoolean(JsonElement value, out bool result)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            result = value.GetBoolean();
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryGetInt(JsonElement value, out int result)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryGetTime(JsonElement value, out TimeOnly result)
    {
        if (value.ValueKind == JsonValueKind.String &&
            TimeOnly.TryParseExact(value.GetString(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryGetTheme(JsonElement value, out AppTheme result)
    {
        if (value.ValueKind == JsonValueKind.String &&
            Enum.TryParse(value.GetString(), ignoreCase: true, out result) &&
            Enum.IsDefined(result))
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric) && Enum.IsDefined(typeof(AppTheme), numeric))
        {
            result = (AppTheme)numeric;
            return true;
        }

        result = default;
        return false;
    }
}

public sealed class NullLogger : IAppLogger
{
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? exception = null) { }
    public Task FlushAsync() => Task.CompletedTask;
}
