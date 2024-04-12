using LantanaGroup.Link.DemoApiGateway.Application.models.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.Commands
{
    public interface ICreateReportScheduledCommand
    {
        Task<string> Execute(ReportScheduled model);
    }
}
