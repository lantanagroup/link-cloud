using LantanaGroup.Link.DemoApiGateway.Application.models.census;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public interface ICensusService
    {
        Task<HttpResponseMessage> GetCensus(string censusId);
        Task<HttpResponseMessage> CreateCensus(CensusConfigModel model);
        Task<HttpResponseMessage> UpdateCensus(string censusId, CensusConfigModel model);
        Task<HttpResponseMessage> DeleteCensus(string censusId);
    }
}
