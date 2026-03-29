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
}
