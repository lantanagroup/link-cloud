using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.ScheduledReport.Queries
{
    public interface IGetScheduledReportQuery
    {
        ScheduledReportEntity Execute(string facilityId);
    }
}
