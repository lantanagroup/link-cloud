using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class LocationHierarchyNode
{
    public required OrganizationLocationMappingModel Mapping { get; set; }

    public List<LocationHierarchyNode> Children { get; set; } = new();

    public int Depth { get; set; }

    public bool IsRoot => Depth == 0;
}
