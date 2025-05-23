using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;

public abstract record ParameterQueryFactoryResult(OperationType opType) : QueryFactoryResult;
