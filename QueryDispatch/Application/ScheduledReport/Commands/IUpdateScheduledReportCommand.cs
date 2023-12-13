using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using StackExchange.Redis;

namespace LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Commands
{
    public interface IUpdateScheduledReportCommand
    {
        Task Execute(ScheduledReportEntity existingReport, ScheduledReportEntity newReport);
    }
}
