using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Commands
{
    public interface ICreateScheduledReportCommand
    {
        Task<string> Execute(ScheduledReportEntity scheduledReport);
    }
}
