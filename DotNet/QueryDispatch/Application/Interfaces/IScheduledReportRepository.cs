using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IScheduledReportRepository : IBaseRepository<ScheduledReportEntity>
    {
        ScheduledReportEntity GetByFacilityId(string facilityId);
        Task Update(ScheduledReportEntity scheduledReport);
    }
}
