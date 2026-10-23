namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// One Link facility and the patient cohorts that flow through it every month.
/// </summary>
public sealed class FacilityDefinition
{
    /// <summary>
    /// The Link facility id. Also the prefix of every shared FHIR resource id (organization, locations, lists).
    /// </summary>
    public string FacilityId { get; set; } = "";

    /// <summary>
    /// Display name used on the Organization and the census lists.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// IANA time zone the facility keeps its calendar in. Month boundaries are local midnight here.
    /// </summary>
    public string TimeZone { get; set; } = "America/Chicago";

    /// <summary>
    /// Short prefix on every generated patient id, so ids read as belonging to this facility.
    /// </summary>
    public string PatientIdPrefix { get; set; } = "";

    /// <summary>
    /// The measures Link is expected to report for this facility. Documentation plus manifest input only;
    /// the simulator never configures Link.
    /// </summary>
    public List<string> Measures { get; set; } = [];

    /// <summary>
    /// The cohorts generated every month.
    /// </summary>
    public List<CohortDefinition> Cohorts { get; set; } = [];

    /// <summary>
    /// Total patients this facility gets per month.
    /// </summary>
    public int PatientsPerMonth => Cohorts.Sum(c => c.Count);

    /// <summary>
    /// Throws when the facility cannot be generated as written.
    /// </summary>
    public void Validate(ScenarioDefinition scenario)
    {
        if (string.IsNullOrWhiteSpace(FacilityId))
        {
            throw new InvalidDataException("Every facility needs a facilityId.");
        }

        if (string.IsNullOrWhiteSpace(PatientIdPrefix))
        {
            throw new InvalidDataException($"Facility '{FacilityId}' needs a patientIdPrefix.");
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidDataException($"Facility '{FacilityId}' has an unknown time zone '{TimeZone}'.", ex);
        }

        var cohortNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cohort in Cohorts)
        {
            cohort.Validate(FacilityId, scenario);
            if (!cohortNames.Add(cohort.Name))
            {
                throw new InvalidDataException($"Facility '{FacilityId}' has two cohorts named '{cohort.Name}'.");
            }
        }
    }
}
