namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

/// <summary>Shape of Data Acquisition's connectionValidation response.</summary>
public sealed class FhirConnectionProbeResult
{
    public bool IsConnected { get; set; }
    public string? ErrorMessage { get; set; }
}
