using DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;

public record SingularParameterQueryFactoryResult(OperationType opType, SearchParams? SearchParams = null, string? ResourceId = null) : ParameterQueryFactoryResult(opType);
