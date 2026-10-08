namespace LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;

/// <summary>
/// An enrollment entry as the Mock DMRP API's support surface returns it.
/// </summary>
public class MockDmrpEntryResponse
{
    /// <summary>
    /// The entry's identifier, a GUID assigned by the mock.
    /// </summary>
    public string Id { get; set; } = string.Empty;

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
    /// <c>Y</c> when the facility reports the measure for the period.
    /// </summary>
    public string IsReporting { get; set; } = string.Empty;

    /// <summary>
    /// When the mock stored the entry.
    /// </summary>
    public DateTimeOffset CreateDate { get; set; }

    /// <summary>
    /// When the mock last changed the entry, or null if it never has.
    /// </summary>
    public DateTimeOffset? ModifyDate { get; set; }
}
