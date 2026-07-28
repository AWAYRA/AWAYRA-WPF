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
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? TimeOnly.MinValue : TimeOnly.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm"));
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

        return MergeWithDefaults(settings);
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
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        legacyOpacity = property.Value.GetDouble();
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
        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name.ToLowerInvariant())
            {
                case "eyeresetenabled":
                    settings.EyeResetEnabled = property.Value.GetBoolean();
                    break;
                case "eyeresetintervalminutes":
                    if (property.Value.ValueKind == JsonValueKind.Number &&
                        property.Value.TryGetInt32(out var eyeInt))
                    {
                        settings.EyeResetIntervalMinutes = eyeInt;
                    }

                    break;
                case "movebreakenabled":
                    settings.MoveBreakEnabled = property.Value.GetBoolean();
                    break;
                case "overlayopacity":
                    if (property.Value.ValueKind == JsonValueKind.Number)
                    {
                        settings.GlassClarity = OverlayGlassSettings.MigrateFromLegacyOpacity(property.Value.GetDouble());
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

    private static AppSettings MergeWithDefaults(AppSettings loaded)
    {
        var defaults = AppSettings.CreateDefault();
        if (loaded.SchemaVersion <= 0)
        {
            loaded.SchemaVersion = AppSettings.CurrentSchemaVersion;
        }

        if (!SettingsValidator.IsValid(loaded))
        {
            if (loaded.EyeResetIntervalMinutes < SettingsValidator.MinIntervalMinutes)
            {
                loaded.EyeResetIntervalMinutes = defaults.EyeResetIntervalMinutes;
            }

            if (loaded.MoveBreakIntervalMinutes < SettingsValidator.MinIntervalMinutes)
            {
                loaded.MoveBreakIntervalMinutes = defaults.MoveBreakIntervalMinutes;
            }
        }

        if (loaded.GlassClarity < OverlayGlassSettings.MinGlassClarity ||
            loaded.GlassClarity > OverlayGlassSettings.MaxGlassClarity)
        {
            loaded.GlassClarity = OverlayGlassSettings.DefaultGlassClarity;
        }
        else
        {
            loaded.GlassClarity = OverlayGlassSettings.NormalizeGlassClarity(loaded.GlassClarity);
        }

        return loaded;
    }
}

public sealed class NullLogger : IAppLogger
{
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? exception = null) { }
    public Task FlushAsync() => Task.CompletedTask;
}
