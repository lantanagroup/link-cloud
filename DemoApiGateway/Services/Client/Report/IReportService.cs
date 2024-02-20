using LantanaGroup.Link.DemoApiGateway.Application.models;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public interface IReportService
    {
        Task<HttpResponseMessage> GetReport(string reportId);
        Task<HttpResponseMessage> CreateReport(ReportConfigModel model);
        Task<HttpResponseMessage> UpdateReport(string reportId, ReportConfigModel model);
        Task<HttpResponseMessage> DeleteReport(string reportId);
        Task<HttpResponseMessage> GetReports(string facilityId);
    }
}
