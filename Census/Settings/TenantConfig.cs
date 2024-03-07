namespace LantanaGroup.Link.Census.Settings;

public class TenantConfig
{
    public string TenantServiceBaseEndpoint { get; set; }
    public string GetTenantRelativeEndpoint { get; set; }
    public bool CheckIfTenantExists { get; set; }
}
