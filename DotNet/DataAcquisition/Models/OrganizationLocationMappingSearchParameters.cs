namespace LantanaGroup.Link.DataAcquisition.Models;

/// <summary>
/// Dedicated lightweight API model for search
/// </summary>
public class OrganizationLocationMappingSearchParameters
{
    public int? LocationMappingId { get; set; }
    public string? LocationId { get; set; }
    public bool? IsOrgLocation { get; set; }
    public bool? IsActive { get; set; }
}
