using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;

public record PagedParameterQueryFactoryResult(OperationType opType, List<SearchParams> SearchParamsList) : ParameterQueryFactoryResult(opType);
