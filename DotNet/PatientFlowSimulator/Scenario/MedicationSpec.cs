namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// A medication ordered at admission (MedicationRequest) and given on a schedule (MedicationAdministration).
/// </summary>
public sealed class MedicationSpec
{
    /// <summary>
    /// RxNorm code. For the hypoglycemic measure it must be in the Diabetes Medications value set.
    /// </summary>
    public string RxNorm { get; set; } = "";

    /// <summary>
    /// Display text of the RxNorm code.
    /// </summary>
    public string Display { get; set; } = "";

    /// <summary>
    /// Dose amount.
    /// </summary>
    public double DoseValue { get; set; }

    /// <summary>
    /// UCUM unit of the dose, e.g. [IU] or mg.
    /// </summary>
    public string DoseUnit { get; set; } = "";

    /// <summary>
    /// SNOMED CT route of administration.
    /// </summary>
    public CodeSpec Route { get; set; } = new()
    {
        System = "http://snomed.info/sct",
        Code = "26643006",
        Display = "Oral route"
    };

    /// <summary>
    /// Hours between two administrations. The first one is two hours after admission.
    /// </summary>
    public int EveryHours { get; set; } = 12;

    /// <summary>
    /// Throws when the medication cannot be generated as written.
    /// </summary>
    public void Validate(string templateName)
    {
        if (string.IsNullOrWhiteSpace(RxNorm) || string.IsNullOrWhiteSpace(DoseUnit))
        {
            throw new InvalidDataException($"Template '{templateName}' has a medication without rxNorm or doseUnit.");
        }

        if (EveryHours < 1)
        {
            throw new InvalidDataException($"Template '{templateName}' medication {RxNorm} needs everyHours >= 1.");
        }
    }
}
