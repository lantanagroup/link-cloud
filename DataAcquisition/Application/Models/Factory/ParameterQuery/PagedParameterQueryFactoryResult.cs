using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;

public record PagedParameterQueryFactoryResult(OperationType opType, List<SearchParams> SearchParamsList) : ParameterQueryFactoryResult(opType);
