namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// A LOINC observation recorded every few hours during a stay, with a random value in a range.
/// </summary>
public sealed class ObservationSpec
{
    /// <summary>
    /// LOINC code.
    /// </summary>
    public string Loinc { get; set; } = "";

    /// <summary>
    /// Display text of the LOINC code.
    /// </summary>
    public string Display { get; set; } = "";

    /// <summary>
    /// FHIR observation category: vital-signs or laboratory.
    /// </summary>
    public string Category { get; set; } = "vital-signs";

    /// <summary>
    /// UCUM unit code, e.g. mg/dL or /min.
    /// </summary>
    public string Unit { get; set; } = "";

    /// <summary>
    /// Lowest generated value.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Highest generated value.
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Hours between two readings. The first reading is one hour after admission.
    /// </summary>
    public int EveryHours { get; set; } = 8;

    /// <summary>
    /// Decimal places kept on the value.
    /// </summary>
    public int Decimals { get; set; }

    /// <summary>
    /// Throws when the observation cannot be generated as written.
    /// </summary>
    public void Validate(string templateName)
    {
        if (string.IsNullOrWhiteSpace(Loinc) || string.IsNullOrWhiteSpace(Unit))
        {
            throw new InvalidDataException($"Template '{templateName}' has an observation without loinc or unit.");
        }

        if (EveryHours < 1 || Max < Min)
        {
            throw new InvalidDataException($"Template '{templateName}' observation {Loinc} needs everyHours >= 1 and min <= max.");
        }
    }
}
