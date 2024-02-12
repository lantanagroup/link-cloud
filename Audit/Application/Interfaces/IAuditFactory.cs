using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditFactory
    {
        CreateAuditEventModel Create(string? facilityId, string? serviceName, string? correlationId, DateTime? eventDate, string? userId, string? user, 
            AuditEventType? action, string? resource, List<PropertyChangeModel>? propertyChanges, string? notes);

        AuditEntity Create(string? facilityId, string? serviceName, string? correlationId, DateTime? eventDate, string? userId, 
            string? user, string? action, string? resource, List<PropertyChangeModel>? propertyChanges, string? notes);
        AuditSearchFilterRecord CreateAuditSearchFilterRecord(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, 
            string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber);
    }
}
