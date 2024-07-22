using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditFactory
    {
        AuditLog Create(string? facilityId, string? serviceName, string? correlationId, DateTime? eventDate, string? userId, 
            string? user, string? action, string? resource, List<PropertyChangeModel>? propertyChanges, string? notes);
        AuditSearchFilterRecord CreateAuditSearchFilterRecord(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, 
            string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber);
    }
}
