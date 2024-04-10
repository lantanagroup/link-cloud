using LantanaGroup.Link.DemoApiGateway.Application.models;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public interface IMeasureDefinitionService
    {
        Task<HttpResponseMessage> GetMeasureDefinition(string MeasureDefinitionId);
        Task<HttpResponseMessage> CreateMeasureDefinition(MeasureDefinitionConfigModel model);
        Task<HttpResponseMessage> UpdateMeasureDefinition(string MeasureDefinitionId, MeasureDefinitionConfigModel model);
        Task<HttpResponseMessage> DeleteMeasureDefinition(string MeasureDefinitionId);
        Task<HttpResponseMessage> GetMeasureDefinitions();
    }
}
