namespace LantanaGroup.Link.Shared.Application.Models.Configs;

public class TenantServiceRegistration
{
    private string? _tenantServiceUrl { get; set; }

    public string? TenantServiceUrl
    {
        get
        {
            if (this._tenantServiceUrl != null && !_tenantServiceUrl.EndsWith("/api"))
                return this._tenantServiceUrl.TrimEnd('/') + "/api";

            return null;
        }
        set
        {
            _tenantServiceUrl = value;
        }

    }
    public bool CheckIfTenantExists { get; set; }
    public string? GetTenantRelativeEndpoint { get; set; }

}
