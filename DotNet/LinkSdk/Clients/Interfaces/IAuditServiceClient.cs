using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Models.Audit;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IAuditServiceClient
{
    /// <summary>Searches audit events: <c>GET /api/audit</c>.</summary>
    Task<LinkApiResponse<PagedAuditApiModel>> SearchAsync(
        string? searchText = null,
        string? facility = null,
        string? correlationId = null,
        string? service = null,
        string? action = null,
        string? user = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one audit event: <c>GET /api/audit/{id}</c>.</summary>
    Task<LinkApiResponse<AuditEventApiModel>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Failure notes in a recent window: <c>GET /api/audit/errors</c>.</summary>
    Task<LinkApiResponse<AuditErrorCount>> GetErrorCountAsync(int hours, CancellationToken cancellationToken = default);
}
