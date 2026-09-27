using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

[BsonIgnoreExtraElements]
public sealed class AutomationMetricsBenchmarkDocument
{
    [BsonId]
    public string Key { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid? ScenarioId { get; set; }

    public Dictionary<string, ThresholdSpec> Thresholds { get; set; } = new(StringComparer.Ordinal);

    public double RegressionPercent { get; set; } = 10;
}

public sealed class ThresholdSpec
{
    public double? Min { get; set; }
    public double? Max { get; set; }
}

public sealed class BenchmarkResultSnapshot
{
    public string? Key { get; set; }
    public bool Pass { get; set; } = true;
    public List<string> Violations { get; set; } = [];
}

public sealed class RegressionResultSnapshot
{
    [BsonRepresentation(BsonType.String)]
    public Guid? PreviousRunId { get; set; }

    public List<string> Flags { get; set; } = [];
}
