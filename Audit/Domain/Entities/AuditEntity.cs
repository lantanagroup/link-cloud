
using LantanaGroup.Link.Audit.Infrastructure.Logging;

namespace LantanaGroup.Link.Audit.Domain.Entities
{
    [BsonCollection("auditEvents")]
    public class AuditEntity : BaseEntity
    {
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
