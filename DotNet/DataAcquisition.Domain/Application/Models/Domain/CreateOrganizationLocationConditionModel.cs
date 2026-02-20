using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

[ExcludeFromCodeCoverage]
public class CreateOrganizationLocationConditionModel
{
    public required string FhirPath { get; set; }
    public int Priority { get; set; } = 1;
}