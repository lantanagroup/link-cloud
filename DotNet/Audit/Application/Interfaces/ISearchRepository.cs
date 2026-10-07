using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Audit;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface ISearchRepository
    {
        Task<(IEnumerable<AuditLog>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);

        Task<AuditErrorCount> CountErrorsAsync(int hours, DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
