using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface ISearchRepository
    {
        Task<(IEnumerable<AuditLog>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }
}
