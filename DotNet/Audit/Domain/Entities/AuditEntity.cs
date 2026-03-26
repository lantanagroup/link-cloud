using LantanaGroup.Link.Audit.Infrastructure.Logging;

namespace LantanaGroup.Link.Audit.Domain.Entities
{
    public readonly record struct AuditId(Guid Value)
    {
        public static AuditId Empty => new(Guid.Empty);
        public static AuditId NewId() => new(Guid.NewGuid());
        public static AuditId FromString(string id) => new(new Guid(id));
    }   
   
    public class AuditLog : BaseEntity
    {
        public long Id { get; set; }
        public AuditId AuditId { get; set; }
        public string? FacilityId { get; set; } = string.Empty;
        public string? ServiceName { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? EventDate { get; set; }
        public string? UserId { get; set; }
        [SensitiveData]
        public string? User { get; set; }
        public string? Action { get; set; }
        public string? Resource { get; set; }
        public List<EntityPropertyChange>? PropertyChanges { get; set; }
        [PiiData]
        public string? Notes { get; set; }       
    }

}
