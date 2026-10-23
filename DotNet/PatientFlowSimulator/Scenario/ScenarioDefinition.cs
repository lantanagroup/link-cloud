using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// The whole simulation, loaded from a scenario JSON file: the facilities, their patient cohorts and the
/// clinical templates the cohorts are generated from. Everything that decides who gets admitted when,
/// and with what data, lives here rather than in code.
/// </summary>
public sealed class ScenarioDefinition
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Short name of the scenario. Recorded in every month manifest.
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    /// Root of every random choice. Change it to get a different, but still repeatable, population.
    /// </summary>
    public string Seed { get; set; } = "patient-flow";

    /// <summary>
    /// How close to the month boundary forced cross-month stays are placed.
    /// </summary>
    public CrossMonthSettings CrossMonth { get; set; } = new();

    /// <summary>
    /// The facilities that get patient flow. Each one is a Link facility.
    /// </summary>
    public List<FacilityDefinition> Facilities { get; set; } = [];

    /// <summary>
    /// Clinical templates referenced by cohorts, keyed by template name.
    /// </summary>
    public Dictionary<string, ClinicalTemplate> Templates { get; set; } = new();

    /// <summary>
    /// Loads and validates a scenario file.
    /// </summary>
    public static ScenarioDefinition Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Scenario file '{path}' was not found.", path);
        }

        using var stream = File.OpenRead(path);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(stream, JsonOptions)
                       ?? throw new InvalidDataException($"Scenario file '{path}' is empty.");
        scenario.Validate();
        return scenario;
    }

    /// <summary>
    /// Finds a template by name, ignoring case.
    /// </summary>
    public ClinicalTemplate GetTemplate(string name)
    {
        foreach (var pair in Templates)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        throw new InvalidDataException($"Template '{name}' is not defined in scenario '{Name}'.");
    }

    /// <summary>
    /// Throws when the scenario references things that do not exist or cannot be generated.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Seed))
        {
            throw new InvalidDataException("Scenario seed is required.");
        }

        if (Facilities.Count == 0)
        {
            throw new InvalidDataException("Scenario defines no facilities.");
        }

        var facilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var facility in Facilities)
        {
            facility.Validate(this);
            if (!facilityIds.Add(facility.FacilityId))
            {
                throw new InvalidDataException($"Facility id '{facility.FacilityId}' is used more than once.");
            }
        }

        foreach (var pair in Templates)
        {
            pair.Value.Validate(pair.Key);
        }
    }
}
