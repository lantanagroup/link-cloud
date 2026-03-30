using LantanaGroup.Link.Automation.Generation;
using System.ComponentModel.DataAnnotations;

namespace Automation.UI.Models;

public class StartScenarioRequest
{
    [Required]
    public AutomationScenarioKind Scenario { get; set; }

    [Range(1, 10000)]
    public int? PatientCount { get; set; }

    [Range(1, 10000)]
    public int? ResourcesPerPatient { get; set; }

    [StringLength(64)]
    public string? PatientPrefix { get; set; }

    [Range(1, int.MaxValue)]
    public int? Seed { get; set; }

    [Range(1, 120)]
    public int? PollingIntervalSeconds { get; set; }

    [Range(1, 5000)]
    public int? MaxRetryCount { get; set; }

    [Range(1, 240)]
    public int? LokiScrapeWindowMinutes { get; set; }

    public bool? RemoveFacilityConfig { get; set; }

    public bool? CleanupTestData { get; set; }

    /// <summary>
    /// Measure selected for this run. Both measure loading and any profile-driven
    /// generation run in this measure context.
    /// </summary>
    public ProfiledMeasureType? SelectedMeasure { get; set; }

    /// <summary>
    /// Per-patient eligibility profiles for measure-eligibility generation mode.
    /// When provided, the Custom scenario uses <c>GenerateWithProfiles()</c> instead
    /// of the standard <c>Generate()</c> code path. Each entry controls whether that
    /// patient qualifies for the measure's Initial Population.
    /// When null or empty, standard random generation is used.
    /// </summary>
    public List<PatientProfile>? PatientProfiles { get; set; }
}
