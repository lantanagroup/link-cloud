using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;

public record SingularParameterQueryFactoryResult(OperationType opType, SearchParams? SearchParams = null, string? ResourceId = null) : ParameterQueryFactoryResult(opType);
