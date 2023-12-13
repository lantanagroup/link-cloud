using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public interface IGetAuditEventListQuery
    {
        Task<PagedAuditModel> Execute(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber);
    }
}
