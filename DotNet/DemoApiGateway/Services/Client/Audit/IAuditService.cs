namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public interface IAuditService
    {
        Task<HttpResponseMessage> ListAuditEvents(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string?
            filterActionBy, Guid? filterUserBy, string? sortBy, int pageSize = 10, int pageNumber = 1);

    }
}
