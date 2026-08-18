using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class UpdateEncounterMappingModel
{
    public bool? MappedToOrg { get; set; }
    public List<int>? OrganizationLocationMappingIds { get; set; }
}
