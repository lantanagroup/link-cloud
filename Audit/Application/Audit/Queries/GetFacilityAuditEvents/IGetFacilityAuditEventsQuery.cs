using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public interface IGetFacilityAuditEventsQuery
    {
        Task <PagedAuditModel> Execute(string facilityId, string? sortBy, SortOrder? sortOrder, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }
}
