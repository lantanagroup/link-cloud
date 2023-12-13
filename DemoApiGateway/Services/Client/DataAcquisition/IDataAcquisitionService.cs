using LantanaGroup.Link.DemoApiGateway.Application.models;
using LantanaGroup.Link.DemoApiGateway.Application.models.dataAcquisition;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client.DataAcquisition
{
    public interface IDataAcquisitionService
    {
        Task<HttpResponseMessage> GetDataAcquisitionConfiguration(string facilityId);
        Task<HttpResponseMessage> CreateDataAcquisitionConfiguration(DataAcquisitionConfigModel model);
        Task<HttpResponseMessage> UpdateDataAcquisitionConfiguration(string facilityId, DataAcquisitionConfigModel model);
        Task<HttpResponseMessage> DeleteDataAcquisitionConfiguration(string facilityId);

        Task<HttpResponseMessage> GetDataAcquisitionQueryConfig(string facilityId);
        Task<HttpResponseMessage> CreateDataAcquisitionQueryConfig(DataAcquisitionQueryConfigModel model);
        Task<HttpResponseMessage> UpdateDataAcquisitionQueryConfig(string facilityId, DataAcquisitionQueryConfigModel model);
        Task<HttpResponseMessage> DeleteDataAcquisitionQueryConfig(string facilityId);

        Task<HttpResponseMessage> GetDataAcquisitionQueryPlan(string facilityId, string queryPlanType);
        Task<HttpResponseMessage> CreateDataAcquisitionQueryPlan(string facilityId, string queryPlanType, DataAcquisitionQueryPlanModel model);
        Task<HttpResponseMessage> UpdateDataAcquisitionQueryPlan(string facilityId, string queryPlanType, DataAcquisitionQueryPlanModel model);
        Task<HttpResponseMessage> DeleteDataAcquisitionQueryPlan(string facilityId, string queryPlanType);

        Task<HttpResponseMessage> GetDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType);
        Task<HttpResponseMessage> CreateDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType, AuthenticationConfiguration model);
        Task<HttpResponseMessage> UpdateDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType, AuthenticationConfiguration model);
        Task<HttpResponseMessage> DeleteDataAcquisitionAuthenticationConfiguration(string facilityId, QueryConfigurationTypePathParameter queryType);
            
    }
}
