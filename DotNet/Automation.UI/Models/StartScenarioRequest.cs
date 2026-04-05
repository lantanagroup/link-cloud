using LantanaGroup.Automation.Generation;
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

    public bool? RemoveFacilityConfig { get; set; }

    public bool? CleanupTestData { get; set; }

    /// <summary>
    /// Single measure for backward compatibility. When set and <see cref="SelectedMeasures"/>
    /// is empty, this measure is used.
    /// </summary>
    public ProfiledMeasureType? SelectedMeasure { get; set; }

    /// <summary>
    /// Measures selected for this run. When multiple measures are selected, the report
    /// is generated with all of them as report types, and qualifying patients must
    /// qualify for every selected measure.
    /// </summary>
    public List<ProfiledMeasureType> SelectedMeasures { get; set; } = [];

    /// <summary>
    /// Per-patient eligibility profiles for measure-eligibility generation mode.
    /// When provided, the Custom scenario uses <c>GenerateWithProfiles()</c> instead
    /// of the standard <c>Generate()</c> code path. Each entry controls whether that
    /// patient qualifies for the measure's Initial Population.
    /// When null or empty, standard random generation is used.
    /// </summary>
    public List<PatientProfile>? PatientProfiles { get; set; }
}
