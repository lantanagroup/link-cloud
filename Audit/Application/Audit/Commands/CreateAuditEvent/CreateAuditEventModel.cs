using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure.Logging;

namespace LantanaGroup.Link.Audit.Application.Commands
{
    public class CreateAuditEventModel
    {
        public string? FacilityId { get; set; }
        public string? ServiceName { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? EventDate { get; set; }
        public string? UserId { get; set; }
        [SensitiveData]
        public string? User { get; set; }
        public string? Action { get; set; }
        public string? Resource { get; set; }
        public List<PropertyChangeModel>? PropertyChanges { get; set; }
        public string? Notes { get; set; }
    } 
}
