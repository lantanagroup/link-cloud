using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;

public interface IResourceIdParameterFactory
{
    Task<ParameterFactoryResult?> Build(ResourceIdsParameter parameter, GetPatientDataRequest request, IDataAcquisitionLogQueries dataAcquisitionLogQueries);
}
