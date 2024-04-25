namespace LantanaGroup.Link.Shared.Application.Models.Configs;

public class TenantServiceRegistration
{
    public string? TenantServiceUrl { get; set; }
    public bool CheckIfTenantExists { get; set; }
    public string? GetTenantRelativeEndpoint { get; set; }

    public string TenantServiceApiUrl
    {
        get
        {
            if (this.TenantServiceUrl != null)
                return this.TenantServiceUrl.TrimEnd('/') + "/api";

            return null;
        }
    }
}
