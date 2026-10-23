namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// The clinical shape of a stay: what kind of encounter it is, how long it lasts and which conditions,
/// observations and medications are recorded during it. A measure's initial population is reached by
/// choosing these values, so adding a measure means adding a template, not code.
/// </summary>
public sealed class ClinicalTemplate
{
    /// <summary>
    /// Free text for people reading the scenario file.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// FHIR v3-ActCode encounter class: IMP, AMB, EMER, OBSENC, NONAC, ACUTE or SS.
    /// </summary>
    public string EncounterClass { get; set; } = "IMP";

    /// <summary>
    /// Optional Encounter.type coding.
    /// </summary>
    public CodeSpec? EncounterType { get; set; }

    /// <summary>
    /// Optional location the encounter happens in. Omit for visits without a ward (clinic visits).
    /// </summary>
    public LocationSpec? Location { get; set; }

    /// <summary>
    /// Length of stay in hours.
    /// </summary>
    public RangeSpec LengthOfStayHours { get; set; } = new() { Min = 24, Max = 96 };

    /// <summary>
    /// When true a patient that is not a forced cross-month patient is discharged before the month ends.
    /// Set false for stays longer than a month.
    /// </summary>
    public bool ConfineToMonth { get; set; } = true;

    /// <summary>
    /// Patient age in years at admission.
    /// </summary>
    public RangeSpec AgeRange { get; set; } = new() { Min = 18, Max = 90 };

    /// <summary>
    /// Discharge disposition recorded on the finished encounter.
    /// </summary>
    public CodeSpec DischargeDisposition { get; set; } = new()
    {
        System = "http://terminology.hl7.org/CodeSystem/discharge-disposition",
        Code = "home",
        Display = "Home"
    };

    /// <summary>
    /// Pool of diagnoses; each stay picks <see cref="ConditionsPerStay"/> of them.
    /// </summary>
    public List<CodeSpec> Conditions { get; set; } = [];

    /// <summary>
    /// How many conditions each stay records.
    /// </summary>
    public int ConditionsPerStay { get; set; } = 1;

    /// <summary>
    /// Observations recorded on a schedule during the stay.
    /// </summary>
    public List<ObservationSpec> Observations { get; set; } = [];

    /// <summary>
    /// Medications ordered at admission and administered on a schedule during the stay.
    /// </summary>
    public List<MedicationSpec> Medications { get; set; } = [];

    /// <summary>
    /// Throws when the template cannot be generated as written.
    /// </summary>
    public void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(EncounterClass))
        {
            throw new InvalidDataException($"Template '{name}' needs an encounterClass.");
        }

        LengthOfStayHours.Validate($"Template '{name}' lengthOfStayHours", minimum: 1);
        AgeRange.Validate($"Template '{name}' ageRange", minimum: 0);

        if (ConditionsPerStay > Conditions.Count)
        {
            throw new InvalidDataException(
                $"Template '{name}' asks for {ConditionsPerStay} conditions per stay but lists {Conditions.Count}.");
        }

        foreach (var observation in Observations)
        {
            observation.Validate(name);
        }

        foreach (var medication in Medications)
        {
            medication.Validate(name);
        }
    }
}
