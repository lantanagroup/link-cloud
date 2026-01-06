namespace LantanaGroup.Link.Shared.Application.Models.Configs;

public class TenantServiceRegistration
{
    public string? TenantServiceUrl { get; set; }
    private string? _publicTenantServiceUrl;
    public string? PublicTenantServiceUrl
    {
        get => string.IsNullOrEmpty(_publicTenantServiceUrl) ? TenantServiceUrl : _publicTenantServiceUrl;
        set => _publicTenantServiceUrl = value;
    }
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
