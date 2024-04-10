using LantanaGroup.Link.DemoApiGateway.Application.models.normalization;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client.Normalization
{
    public interface INormalizationService
    {
        Task<HttpResponseMessage> CreateNormalizationConfig(NormalizationConfigModel model);
        Task<HttpResponseMessage> GetNormalizationConfig(string facilityId);
        Task<HttpResponseMessage> UpdateNormalizationConfig(string facilityId, NormalizationConfigModel model);
        Task<HttpResponseMessage> DeleteNormalizationConfig(string facilityId);
        
    }
}
