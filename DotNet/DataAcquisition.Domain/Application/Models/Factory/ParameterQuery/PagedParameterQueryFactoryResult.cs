using DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;

public record PagedParameterQueryFactoryResult(OperationType opType, List<SearchParams> SearchParamsList) : ParameterQueryFactoryResult(opType);
