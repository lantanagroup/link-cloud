using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Audit.Application.Models
{
    public record AuditModel
    {
        public string Id { get; set; } = null!;
        public string? FacilityId { get; set; }
        public string? CorrelationId { get; set; }
        public string? ServiceName { get; set; }
        public DateTime? EventDate { get; set; }
        public string? User { get; set; }
        public string? Action { get; set; }
        public string? Resource { get; set; }
        public List<PropertyChangeModel>? PropertyChanges { get; set; }
        public string? Notes { get; set; }       
        
        public static AuditModel FromDomain(AuditLog log)
        {
            return new AuditModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = log.FacilityId,
                CorrelationId = log.CorrelationId,
                ServiceName = log.ServiceName,
                EventDate = log.EventDate,
                User = log.User,
                Action = log.Action,
                Resource = log.Resource,
                PropertyChanges = log.PropertyChanges?.Select(p => new PropertyChangeModel { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList(),
                Notes = log.Notes
            };
        }
    }
}
