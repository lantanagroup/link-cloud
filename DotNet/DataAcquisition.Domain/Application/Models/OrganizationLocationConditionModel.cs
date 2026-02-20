using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class OrganizationLocationConditionModel
{
    public int ConditionId { get; set; }
    public string? FhirPath { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}
