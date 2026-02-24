using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class OrganizationLocationMappingModel
{
    public int LocationMappingId { get; set; }

    public string? FacilityId { get; set; }

    public string? LocationId { get; set; }

    public string? LocationName { get; set; }

    public string? LocationAlias { get; set; }

    public string? PartOfValue { get; set; }

    public int? PartOfId { get; set; }

    public bool IsOrgLocation { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    public bool IsActive { get; set; }
}
