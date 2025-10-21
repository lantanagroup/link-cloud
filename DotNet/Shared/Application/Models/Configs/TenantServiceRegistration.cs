namespace LantanaGroup.Link.Shared.Application.Models.Configs;

public class TenantServiceRegistration
{
    public string? TenantServiceUrl { get; set; }
    public string? PublicTenantServiceUrl { get; set; }
    public bool CheckIfTenantExists { get; set; }
    public string? GetTenantRelativeEndpoint { get; set; }

    public string? PublicTenantServiceApiUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(this.PublicTenantServiceUrl))
                return this.PublicTenantServiceUrl.TrimEnd('/') + "/api";

            return null;
        }
    }
}
