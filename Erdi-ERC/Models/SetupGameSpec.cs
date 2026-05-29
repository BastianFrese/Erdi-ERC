using System.Globalization;
using System.Text.Json;

namespace <OWNER_HANDLE>_ERC.Models;

public sealed class SetupEditorConfig
{
    public int PayloadVersion { get; init; }
    public string PayloadSource { get; init; } = string.Empty;
    public IReadOnlyList<SetupCategoryConfig> Categories { get; init; } = Array.Empty<SetupCategoryConfig>();
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Presets { get; init; } = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<SetupRaceStrategyConfig> RaceStrategies { get; init; } = Array.Empty<SetupRaceStrategyConfig>();
}

public sealed class SetupRaceStrategyConfig
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Distance { get; init; }
    public string Icon { get; init; } = "🏁";
    public string Description { get; init; } = string.Empty;
}

public sealed class SetupCategoryConfig
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Icon { get; init; }
    public IReadOnlyList<SetupFieldConfig> Fields { get; init; } = Array.Empty<SetupFieldConfig>();
}

public sealed class SetupFieldConfig
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Step { get; init; }
    public double DefaultValue { get; init; }
}

public sealed class SetupMetricConfig
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public double BaseValue { get; init; }
    public IReadOnlyList<SetupMetricTerm> Terms { get; init; } = Array.Empty<SetupMetricTerm>();
}

public sealed class SetupMetricTerm
{
    public required string Category { get; init; }
    public required string Field { get; init; }
    public double Coefficient { get; init; }
}

public sealed class SetupNormalizationResult
{
    public bool Success { get; init; }
    public string NormalizedPayload { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool Migrated { get; init; }
}

public static class SetupGameSpec
{
    private const int StrictValidationFromVersion = 2;
    private const string PayloadSourcePrefix = "f1-ingame-style-editor-v";

    private static readonly SetupEditorConfig EditorConfig = new()
    {
        PayloadVersion = 1,
        PayloadSource = BuildPayloadSource(1),
        Categories =
        [
            new SetupCategoryConfig
            {
                Key = "aero",
                Label = "Aerodynamik",
                Icon = "🌀",
                Fields =
                [
                    new SetupFieldConfig { Key = "frontWing", Label = "Front Wing Aero", Min = 0, Max = 50, Step = 1, DefaultValue = 24 },
                    new SetupFieldConfig { Key = "rearWing", Label = "Rear Wing Aero", Min = 0, Max = 50, Step = 1, DefaultValue = 21 }
                ]
            },
            new SetupCategoryConfig
            {
                Key = "transmission",
                Label = "Getriebe",
                Icon = "⚙️",
                Fields =
                [
                    new SetupFieldConfig { Key = "diffOnThrottle", Label = "Differential Adjustment On Throttle", Min = 10, Max = 100, Step = 1, DefaultValue = 55 },
                    new SetupFieldConfig { Key = "diffOffThrottle", Label = "Differential Adjustment Off Throttle", Min = 10, Max = 100, Step = 1, DefaultValue = 52 }
                ]
            },
            new SetupCategoryConfig
            {
                Key = "geometry",
                Label = "Fahrwerksgeometrie",
                Icon = "📐",
                Fields =
                [
                    new SetupFieldConfig { Key = "frontCamber", Label = "Front Camber", Min = -3.50, Max = -2.50, Step = 0.01, DefaultValue = -3.50 },
                    new SetupFieldConfig { Key = "rearCamber", Label = "Rear Camber", Min = -2.00, Max = -1.00, Step = 0.01, DefaultValue = -1.80 },
                    new SetupFieldConfig { Key = "frontToe", Label = "Front Toe-Out", Min = 0.00, Max = 0.20, Step = 0.01, DefaultValue = 0.08 },
                    new SetupFieldConfig { Key = "rearToe", Label = "Rear Toe-In", Min = 0.10, Max = 0.25, Step = 0.01, DefaultValue = 0.20 }
                ]
            },
            new SetupCategoryConfig
            {
                Key = "suspension",
                Label = "Federung",
                Icon = "🧱",
                Fields =
                [
                    new SetupFieldConfig { Key = "frontSuspension", Label = "Front Suspension", Min = 1, Max = 41, Step = 1, DefaultValue = 16 },
                    new SetupFieldConfig { Key = "rearSuspension", Label = "Rear Suspension", Min = 1, Max = 41, Step = 1, DefaultValue = 9 },
                    new SetupFieldConfig { Key = "frontAntiRoll", Label = "Front Anti-Roll Bar", Min = 1, Max = 21, Step = 1, DefaultValue = 8 },
                    new SetupFieldConfig { Key = "rearAntiRoll", Label = "Rear Anti-Roll Bar", Min = 1, Max = 21, Step = 1, DefaultValue = 5 },
                    new SetupFieldConfig { Key = "frontRideHeight", Label = "Front Ride Height", Min = 15, Max = 35, Step = 1, DefaultValue = 22 },
                    new SetupFieldConfig { Key = "rearRideHeight", Label = "Rear Ride Height", Min = 40, Max = 60, Step = 1, DefaultValue = 50 }
                ]
            },
            new SetupCategoryConfig
            {
                Key = "brakes",
                Label = "Bremsen",
                Icon = "🛑",
                Fields =
                [
                    new SetupFieldConfig { Key = "brakePressure", Label = "Brake Pressure", Min = 80, Max = 100, Step = 1, DefaultValue = 100 },
                    new SetupFieldConfig { Key = "brakeBias", Label = "Front Brake Bias", Min = 50, Max = 70, Step = 1, DefaultValue = 56 }
                ]
            },
            new SetupCategoryConfig
            {
                Key = "tyres",
                Label = "Reifen",
                Icon = "🛞",
                Fields =
                [
                    new SetupFieldConfig { Key = "frontRightPressure", Label = "Front Right Tyre Pressure", Min = 22.5, Max = 29.5, Step = 0.1, DefaultValue = 23.2 },
                    new SetupFieldConfig { Key = "frontLeftPressure", Label = "Front Left Tyre Pressure", Min = 22.5, Max = 29.5, Step = 0.1, DefaultValue = 23.2 },
                    new SetupFieldConfig { Key = "rearRightPressure", Label = "Rear Right Tyre Pressure", Min = 20.5, Max = 26.5, Step = 0.1, DefaultValue = 21.0 },
                    new SetupFieldConfig { Key = "rearLeftPressure", Label = "Rear Left Tyre Pressure", Min = 20.5, Max = 26.5, Step = 0.1, DefaultValue = 21.0 }
                ]
            }
        ],
        Presets = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["quali"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["frontWing"] = 18, ["rearWing"] = 15, ["diffOnThrottle"] = 62, ["diffOffThrottle"] = 55,
                ["frontCamber"] = -3.5, ["rearCamber"] = -1.8, ["frontToe"] = 0.1, ["rearToe"] = 0.20,
                ["frontSuspension"] = 18, ["rearSuspension"] = 9, ["frontAntiRoll"] = 10, ["rearAntiRoll"] = 5, ["frontRideHeight"] = 20, ["rearRideHeight"] = 45,
                ["brakePressure"] = 100, ["brakeBias"] = 55,
                ["frontRightPressure"] = 23.4, ["frontLeftPressure"] = 23.4, ["rearRightPressure"] = 21.2, ["rearLeftPressure"] = 21.2
            },
            ["race"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["frontWing"] = 26, ["rearWing"] = 23, ["diffOnThrottle"] = 56, ["diffOffThrottle"] = 53,
                ["frontCamber"] = -3.5, ["rearCamber"] = -1.8, ["frontToe"] = 0.08, ["rearToe"] = 0.22,
                ["frontSuspension"] = 16, ["rearSuspension"] = 10, ["frontAntiRoll"] = 8, ["rearAntiRoll"] = 5, ["frontRideHeight"] = 22, ["rearRideHeight"] = 50,
                ["brakePressure"] = 100, ["brakeBias"] = 56,
                ["frontRightPressure"] = 23.2, ["frontLeftPressure"] = 23.2, ["rearRightPressure"] = 21.0, ["rearLeftPressure"] = 21.0
            },
            ["wet"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["frontWing"] = 34, ["rearWing"] = 32, ["diffOnThrottle"] = 52, ["diffOffThrottle"] = 50,
                ["frontCamber"] = -3.4, ["rearCamber"] = -1.65, ["frontToe"] = 0.06, ["rearToe"] = 0.24,
                ["frontSuspension"] = 13, ["rearSuspension"] = 9, ["frontAntiRoll"] = 6, ["rearAntiRoll"] = 4, ["frontRideHeight"] = 28, ["rearRideHeight"] = 58,
                ["brakePressure"] = 97, ["brakeBias"] = 54,
                ["frontRightPressure"] = 22.8, ["frontLeftPressure"] = 22.8, ["rearRightPressure"] = 20.8, ["rearLeftPressure"] = 20.8
            },
            // F1 25 Rennstrategien nach Renndistanz – Setups sind auf typische Reifenstints und Spritlast abgestimmt.
            ["sprint5"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["frontWing"] = 22, ["rearWing"] = 18, ["diffOnThrottle"] = 60, ["diffOffThrottle"] = 55,
                ["frontCamber"] = -3.5, ["rearCamber"] = -1.85, ["frontToe"] = 0.10, ["rearToe"] = 0.20,
                ["frontSuspension"] = 18, ["rearSuspension"] = 10, ["frontAntiRoll"] = 10, ["rearAntiRoll"] = 6, ["frontRideHeight"] = 20, ["rearRideHeight"] = 46,
                ["brakePressure"] = 100, ["brakeBias"] = 55,
                ["frontRightPressure"] = 23.4, ["frontLeftPressure"] = 23.4, ["rearRightPressure"] = 21.2, ["rearLeftPressure"] = 21.2
            },
            ["race25"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["frontWing"] = 24, ["rearWing"] = 21, ["diffOnThrottle"] = 58, ["diffOffThrottle"] = 54,
                ["frontCamber"] = -3.5, ["rearCamber"] = -1.8, ["frontToe"] = 0.09, ["rearToe"] = 0.21,
                ["frontSuspension"] = 17, ["rearSuspension"] = 10, ["frontAntiRoll"] = 9, ["rearAntiRoll"] = 5, ["frontRideHeight"] = 21, ["rearRideHeight"] = 48,
                ["brakePressure"] = 100, ["brakeBias"] = 56,
                ["frontRightPressure"] = 23.3, ["frontLeftPressure"] = 23.3, ["rearRightPressure"] = 21.1, ["rearLeftPressure"] = 21.1
            },
            ["race50"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["frontWing"] = 26, ["rearWing"] = 23, ["diffOnThrottle"] = 56, ["diffOffThrottle"] = 53,
                ["frontCamber"] = -3.4, ["rearCamber"] = -1.75, ["frontToe"] = 0.08, ["rearToe"] = 0.22,
                ["frontSuspension"] = 15, ["rearSuspension"] = 9, ["frontAntiRoll"] = 8, ["rearAntiRoll"] = 5, ["frontRideHeight"] = 23, ["rearRideHeight"] = 50,
                ["brakePressure"] = 99, ["brakeBias"] = 56,
                ["frontRightPressure"] = 23.0, ["frontLeftPressure"] = 23.0, ["rearRightPressure"] = 20.9, ["rearLeftPressure"] = 20.9
            },
            ["race100"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["frontWing"] = 28, ["rearWing"] = 25, ["diffOnThrottle"] = 54, ["diffOffThrottle"] = 52,
                ["frontCamber"] = -3.3, ["rearCamber"] = -1.7, ["frontToe"] = 0.07, ["rearToe"] = 0.23,
                ["frontSuspension"] = 14, ["rearSuspension"] = 8, ["frontAntiRoll"] = 7, ["rearAntiRoll"] = 4, ["frontRideHeight"] = 25, ["rearRideHeight"] = 52,
                ["brakePressure"] = 98, ["brakeBias"] = 57,
                ["frontRightPressure"] = 22.8, ["frontLeftPressure"] = 22.8, ["rearRightPressure"] = 20.7, ["rearLeftPressure"] = 20.7
            }
        },
        RaceStrategies =
        [
            new SetupRaceStrategyConfig { Key = "sprint5",  Label = "5 Runden Sprint",  Distance = "5 Laps",  Icon = "⚡", Description = "Quick Race / Show Race – maximaler Push, kaum Reifenmanagement." },
            new SetupRaceStrategyConfig { Key = "race25",   Label = "25% Distanz",      Distance = "25%",     Icon = "🟢", Description = "Kurzes Rennen, meist No-Stop oder ein optionaler Stopp." },
            new SetupRaceStrategyConfig { Key = "race50",   Label = "50% Distanz",      Distance = "50%",     Icon = "🟡", Description = "Klassische Liga-Distanz mit 1–2 Boxenstopps." },
            new SetupRaceStrategyConfig { Key = "race100",  Label = "100% Distanz",     Distance = "100%",    Icon = "🔴", Description = "Volle Renndistanz mit hoher Spritlast und Reifenschonung." }
        ]
    };

    private static readonly IReadOnlyList<SetupMetricConfig> MetricConfig =
    [
        new SetupMetricConfig
        {
            Key = "topSpeed",
            Label = "Top Speed",
            BaseValue = 107,
            Terms =
            [
                new SetupMetricTerm { Category = "aero", Field = "frontWing", Coefficient = -0.45 },
                new SetupMetricTerm { Category = "aero", Field = "rearWing", Coefficient = -0.9 },
                new SetupMetricTerm { Category = "transmission", Field = "diffOffThrottle", Coefficient = -0.12 },
                new SetupMetricTerm { Category = "suspension", Field = "frontRideHeight", Coefficient = 0.35 },
                new SetupMetricTerm { Category = "suspension", Field = "rearRideHeight", Coefficient = -0.35 }
            ]
        },
        new SetupMetricConfig
        {
            Key = "traction",
            Label = "Traktion",
            BaseValue = 22,
            Terms =
            [
                new SetupMetricTerm { Category = "aero", Field = "rearWing", Coefficient = 1.2 },
                new SetupMetricTerm { Category = "transmission", Field = "diffOnThrottle", Coefficient = 0.22 },
                new SetupMetricTerm { Category = "suspension", Field = "rearSuspension", Coefficient = 0.55 },
                new SetupMetricTerm { Category = "suspension", Field = "rearAntiRoll", Coefficient = -0.45 }
            ]
        },
        new SetupMetricConfig
        {
            Key = "rotation",
            Label = "Einlenken",
            BaseValue = 99,
            Terms =
            [
                new SetupMetricTerm { Category = "aero", Field = "frontWing", Coefficient = 0.85 },
                new SetupMetricTerm { Category = "geometry", Field = "frontToe", Coefficient = 85 },
                new SetupMetricTerm { Category = "brakes", Field = "brakeBias", Coefficient = -1.15 },
                new SetupMetricTerm { Category = "suspension", Field = "frontAntiRoll", Coefficient = 0.42 },
                new SetupMetricTerm { Category = "suspension", Field = "rearRideHeight", Coefficient = -0.2 }
            ]
        },
        new SetupMetricConfig
        {
            Key = "stability",
            Label = "Stabilität",
            BaseValue = 26,
            Terms =
            [
                new SetupMetricTerm { Category = "aero", Field = "rearWing", Coefficient = 1.05 },
                new SetupMetricTerm { Category = "brakes", Field = "brakeBias", Coefficient = 0.78 },
                new SetupMetricTerm { Category = "brakes", Field = "brakePressure", Coefficient = 0.18 },
                new SetupMetricTerm { Category = "suspension", Field = "rearRideHeight", Coefficient = 0.2 },
                new SetupMetricTerm { Category = "geometry", Field = "frontToe", Coefficient = -62 },
                new SetupMetricTerm { Category = "suspension", Field = "frontSuspension", Coefficient = -0.38 }
            ]
        },
        new SetupMetricConfig
        {
            Key = "tyreWear",
            Label = "Reifenschonung",
            BaseValue = 289.2,
            Terms =
            [
                new SetupMetricTerm { Category = "tyres", Field = "frontRightPressure", Coefficient = -2.2 },
                new SetupMetricTerm { Category = "tyres", Field = "frontLeftPressure", Coefficient = -2.2 },
                new SetupMetricTerm { Category = "tyres", Field = "rearRightPressure", Coefficient = -2.2 },
                new SetupMetricTerm { Category = "tyres", Field = "rearLeftPressure", Coefficient = -2.2 },
                new SetupMetricTerm { Category = "geometry", Field = "frontToe", Coefficient = -65 },
                new SetupMetricTerm { Category = "geometry", Field = "rearToe", Coefficient = -42 },
                new SetupMetricTerm { Category = "transmission", Field = "diffOnThrottle", Coefficient = -0.06 }
            ]
        }
    ];

    private static readonly Dictionary<string, Dictionary<string, SetupFieldConfig>> RulesByCategory = EditorConfig.Categories
        .ToDictionary(
            c => c.Key,
            c => c.Fields.ToDictionary(f => f.Key, f => f, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

    public static SetupEditorConfig GetEditorConfig() => EditorConfig;
    public static IReadOnlyList<SetupMetricConfig> GetMetricConfig() => MetricConfig;
    public static string BuildPayloadSource(int version)
    {
        var safeVersion = Math.Max(1, version);
        return $"{PayloadSourcePrefix}{safeVersion}";
    }

    public static int ReadPayloadVersion(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return 1;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return 1;
            }

            if (root.TryGetProperty("version", out var versionElement)
                && versionElement.ValueKind == JsonValueKind.Number)
            {
                var value = versionElement.GetInt32();
                return value > 0 ? value : 1;
            }
        }
        catch
        {
            // ignore malformed legacy payloads
        }

        return 1;
    }

    public static IReadOnlyDictionary<string, string> GetCategoryDisplayNames()
        => EditorConfig.Categories.ToDictionary(c => c.Key, c => c.Label, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> GetFieldDisplayNames()
        => EditorConfig.Categories
            .SelectMany(c => c.Fields)
            .ToDictionary(f => f.Key, f => f.Label, StringComparer.OrdinalIgnoreCase);

    public static SetupNormalizationResult NormalizePayload(string rawPayload, int targetVersion)
    {
        var safeTargetVersion = Math.Max(1, targetVersion);

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return new SetupNormalizationResult { Success = false, Error = "Payload ist leer." };
        }

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new SetupNormalizationResult { Success = false, Error = "Payload muss ein JSON-Objekt sein." };
            }

            var inputVersion = ReadPayloadVersion(rawPayload);

            JsonElement categories;
            if (root.TryGetProperty("categories", out var categoriesElement) && categoriesElement.ValueKind == JsonValueKind.Object)
            {
                categories = categoriesElement;
            }
            else
            {
                categories = root;
            }

            foreach (var incomingCategory in categories.EnumerateObject())
            {
                if (incomingCategory.NameEquals("version") || incomingCategory.NameEquals("source") || incomingCategory.NameEquals("categories")
                    || incomingCategory.NameEquals("strategy") || incomingCategory.NameEquals("trackKey")
                    || incomingCategory.NameEquals("lengthKey") || incomingCategory.NameEquals("strategyPlan"))
                {
                    continue;
                }

                if (!RulesByCategory.ContainsKey(incomingCategory.Name))
                {
                    return new SetupNormalizationResult { Success = false, Error = $"Unbekannte Kategorie '{incomingCategory.Name}'." };
                }
            }

            var normalizedCategories = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
            var migrated = inputVersion != safeTargetVersion;

            foreach (var category in EditorConfig.Categories)
            {
                if (!categories.TryGetProperty(category.Key, out var categoryElement) || categoryElement.ValueKind != JsonValueKind.Object)
                {
                    if (inputVersion < StrictValidationFromVersion)
                    {
                        categoryElement = default;
                        migrated = true;
                    }
                    else
                    {
                        return new SetupNormalizationResult { Success = false, Error = $"Kategorie '{category.Key}' fehlt." };
                    }
                }

                if (categoryElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var incomingValue in categoryElement.EnumerateObject())
                    {
                        if (!RulesByCategory[category.Key].ContainsKey(incomingValue.Name))
                        {
                            if (category.Key.Equals("transmission", StringComparison.OrdinalIgnoreCase)
                                && incomingValue.Name.Equals("engineBraking", StringComparison.OrdinalIgnoreCase))
                            {
                                migrated = true;
                                continue;
                            }

                            return new SetupNormalizationResult { Success = false, Error = $"Unbekannter Wert '{incomingValue.Name}' in Kategorie '{category.Key}'." };
                        }
                    }
                }

                var normalizedValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                foreach (var rule in category.Fields)
                {
                    var rawValue = rule.DefaultValue;
                    var hasValue = categoryElement.ValueKind == JsonValueKind.Object
                        && categoryElement.TryGetProperty(rule.Key, out var valueElement)
                        && TryReadNumericValue(valueElement, out rawValue);

                    if (!hasValue)
                    {
                        if (inputVersion >= StrictValidationFromVersion)
                        {
                            return new SetupNormalizationResult { Success = false, Error = $"Wert '{rule.Key}' fehlt oder ist ungültig in Kategorie '{category.Key}'." };
                        }

                        rawValue = rule.DefaultValue;
                        migrated = true;
                    }

                    normalizedValues[rule.Key] = SnapAndClamp(rawValue, rule.Min, rule.Max, rule.Step);
                }

                normalizedCategories[category.Key] = normalizedValues;
            }

            var payload = new
            {
                version = safeTargetVersion,
                source = BuildPayloadSource(safeTargetVersion),
                strategy = ReadStrategyKey(root),
                trackKey = ReadStringField(root, "trackKey"),
                lengthKey = ReadStringField(root, "lengthKey"),
                strategyPlan = ReadStringField(root, "strategyPlan", maxLength: 4000),
                categories = normalizedCategories
            };

            return new SetupNormalizationResult
            {
                Success = true,
                NormalizedPayload = JsonSerializer.Serialize(payload),
                Migrated = migrated
            };
        }
        catch (JsonException)
        {
            return new SetupNormalizationResult { Success = false, Error = "JSON-Format ungültig." };
        }
    }

    public static Dictionary<string, double> CalculateMetricScores(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> categories)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var metric in MetricConfig)
        {
            var value = metric.BaseValue;
            foreach (var term in metric.Terms)
            {
                var fieldValue = 0d;
                if (categories.TryGetValue(term.Category, out var fields)
                    && fields.TryGetValue(term.Field, out var parsed))
                {
                    fieldValue = parsed;
                }

                value += fieldValue * term.Coefficient;
            }

            scores[metric.Key] = Math.Min(100, Math.Max(0, value));
        }

        return scores;
    }

    private static bool TryReadNumericValue(JsonElement valueElement, out double value)
    {
        if (valueElement.ValueKind == JsonValueKind.Number)
        {
            return valueElement.TryGetDouble(out value);
        }

        if (valueElement.ValueKind == JsonValueKind.String)
        {
            var text = valueElement.GetString();
            if (!string.IsNullOrWhiteSpace(text)
                && double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static double SnapAndClamp(double value, double min, double max, double step)
    {
        var clamped = Math.Min(max, Math.Max(min, value));
        var steps = Math.Round((clamped - min) / step, MidpointRounding.AwayFromZero);
        var snapped = min + (steps * step);
        var decimals = StepDecimals(step);
        snapped = Math.Round(snapped, decimals, MidpointRounding.AwayFromZero);
        return Math.Min(max, Math.Max(min, snapped));
    }

    private static int StepDecimals(double step)
    {
        var text = step.ToString(CultureInfo.InvariantCulture);
        var idx = text.IndexOf('.');
        return idx < 0 ? 0 : text.Length - idx - 1;
    }

    private static readonly HashSet<string> KnownStrategyKeys =
        new(EditorConfig.RaceStrategies.Select(s => s.Key), StringComparer.OrdinalIgnoreCase);

    private static string? ReadStrategyKey(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("strategy", out var strategyElement)) return null;
        if (strategyElement.ValueKind != JsonValueKind.String) return null;
        var raw = strategyElement.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return KnownStrategyKeys.Contains(raw) ? raw : null;
    }

    public static string? ReadStrategyKey(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            return ReadStrategyKey(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadStringField(JsonElement root, string name, int maxLength = 128)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return null;
        var raw = element.GetString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (trimmed.Length > maxLength) trimmed = trimmed[..maxLength];
        return trimmed;
    }

    public static (string? TrackKey, string? LengthKey, string? StrategyPlan) ReadStrategyContext(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload)) return (null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            return (ReadStringField(root, "trackKey"), ReadStringField(root, "lengthKey"), ReadStringField(root, "strategyPlan", 4000));
        }
        catch
        {
            return (null, null, null);
        }
    }
}
