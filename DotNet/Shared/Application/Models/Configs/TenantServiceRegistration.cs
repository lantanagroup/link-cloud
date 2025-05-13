namespace LantanaGroup.Link.Shared.Application.Models.Configs;

public class TenantServiceRegistration
{
    public string? TenantServiceUrl { get; set; }
    public bool CheckIfTenantExists { get; set; }
    public string? GetTenantRelativeEndpoint { get; set; }

}
