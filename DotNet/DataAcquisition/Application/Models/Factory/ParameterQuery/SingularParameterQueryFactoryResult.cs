using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;

public record SingularParameterQueryFactoryResult(OperationType opType, SearchParams? SearchParams = null, string? ResourceId = null) : ParameterQueryFactoryResult(opType);
