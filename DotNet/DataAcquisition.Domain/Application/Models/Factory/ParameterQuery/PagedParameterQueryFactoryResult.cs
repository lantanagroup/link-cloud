using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;

public record PagedParameterQueryFactoryResult(OperationType opType, List<List<KeyValuePair<string, string>>> SearchParamsList) : ParameterQueryFactoryResult(opType);
