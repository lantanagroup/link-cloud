﻿using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Audit.Application.Models
{
    public class AuditModel
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
    }
}
