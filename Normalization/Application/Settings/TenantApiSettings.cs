namespace LantanaGroup.Link.Normalization.Application.Settings;

public class TenantApiSettings
{
    public string TenantServiceBaseEndpoint { get; set; }
    public bool CheckIfFacilityExists { get; set; }
    public string GetTenantRelativeEndpoint { get; set; }
}
