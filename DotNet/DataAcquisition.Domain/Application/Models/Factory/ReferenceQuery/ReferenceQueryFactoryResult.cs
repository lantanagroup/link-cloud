using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;

public record ReferenceQueryFactoryResult(string ResourceType, List<ResourceReference> ReferenceIds) : QueryFactoryResult;