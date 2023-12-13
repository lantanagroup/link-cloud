namespace LantanaGroup.Link.DemoApiGateway.Application.models.audit
{
    public class AuditModel
    {
        public Guid? Id { get; set; }
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
