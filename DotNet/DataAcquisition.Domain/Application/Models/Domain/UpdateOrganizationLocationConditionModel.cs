using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

[ExcludeFromCodeCoverage]
public class UpdateOrganizationLocationConditionModel
{
    public required string FhirPath { get; set; }
    public required int Priority { get; set; } = 1;
}