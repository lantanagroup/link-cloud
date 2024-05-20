using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IScheduledReportRepository : IPersistenceRepository<ScheduledReportEntity>
    {
        ScheduledReportEntity GetByFacilityId(string facilityId);
        Task Update(ScheduledReportEntity scheduledReport);
    }
}
