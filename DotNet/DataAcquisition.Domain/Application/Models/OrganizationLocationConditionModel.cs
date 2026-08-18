using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[ExcludeFromCodeCoverage]
public class OrganizationLocationConditionModel
{
    public int ConditionId { get; set; }
    public string? FhirPath { get; set; }
    
    /// <summary>
    /// The order that conditions are evaluated in.  Since all conditions are an OR, this is only used for performance in evaluations.
    /// </summary>
    public int Priority { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
}
