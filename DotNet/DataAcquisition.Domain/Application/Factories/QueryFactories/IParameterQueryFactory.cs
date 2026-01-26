using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;

public interface IParameterQueryFactory
{
    Task<ParameterQueryFactoryResult> Build(ParameterQueryConfig config, GetPatientDataRequest request, ScheduledReport scheduledReport, string lookback, IDataAcquisitionLogQueries dataAcquisitionLogQueries);
}
