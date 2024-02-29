namespace LantanaGroup.Link.Shared.Application.Models.Configs;

public class TenantApiSettings
{
    public string TenantServiceBaseEndpoint { get; set; }
    public bool CheckIfTenantExists { get; set; }
    public string GetTenantRelativeEndpoint { get; set; }
}
