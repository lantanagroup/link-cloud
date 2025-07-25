namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

public class ApiSettings
{
    public FhirListSettings? FhirListSettings { get; set; }
}

public class FhirListSettings
{
    public List<string>? ValidStatuses { get; set; }
    public List<string>? ValidTimeFrames { get; set; }
}
