namespace LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;

/// <summary>
/// An enrollment entry to create on the Mock DMRP API's support surface (<c>POST /api/mock-dmrp/entries</c>).
/// </summary>
/// <remarks>
/// The mock's own support contract, not the real DMRP API's. Kept apart from
/// <c>Integration/DMRP</c>, where the real contracts live, so neither can be mistaken for the other.
/// </remarks>
public class MockDmrpEntryRequest
{
    /// <summary>
    /// The facility the entry enrolls, as Link knows it (the NHSN Org ID).
    /// </summary>
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// The NHSN component, <c>MSC</c> or <c>PS</c>.
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// The NHSN measure, for example <c>HOB</c>.
    /// </summary>
    public string Measure { get; set; } = string.Empty;

    /// <summary>
    /// Month of the reporting period, 1-12.
    /// </summary>
    public int ReportingMonth { get; set; }

    /// <summary>
    /// Year of the reporting period.
    /// </summary>
    public int ReportingYear { get; set; }

    /// <summary>
    /// <c>Y</c> when the facility reports the measure for the period. The mock serves only <c>Y</c>
    /// entries from its contract endpoints.
    /// </summary>
    public string IsReporting { get; set; } = "Y";
}
