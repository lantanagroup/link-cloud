using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public interface IGetFacilityAuditEventsQuery
    {
        Task <PagedAuditModel> Execute(string facilityId, int pageNumber, int pageSize);
    }
}
