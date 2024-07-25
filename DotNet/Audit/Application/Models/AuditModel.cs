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
                Id = log.AuditId.Value.ToString(),
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

        public static AuditModel FromMessage(AuditEventMessage message)
        {
            var model =  new AuditModel
            {                
                FacilityId = message.FacilityId,
                CorrelationId = message.CorrelationId,
                ServiceName = message.ServiceName,
                EventDate = message.EventDate,
                User = message.User,
                Action = message.Action.ToString(),
                Resource = message.Resource,              
                Notes = message.Notes
            };

            if (message.PropertyChanges is not null)
            {
                if (model.PropertyChanges is not null)
                    model.PropertyChanges.Clear();
                else
                    model.PropertyChanges = new List<PropertyChangeModel>();

                model.PropertyChanges.AddRange(message.PropertyChanges);
            }

            return model;
        }

        public static AuditLog ToDomain(AuditModel model)
        {
            return new AuditLog
            {
                AuditId = string.IsNullOrEmpty(model.Id) ? AuditId.NewId() : AuditId.FromString(model.Id),
                FacilityId = model.FacilityId,
                CorrelationId = model.CorrelationId,
                ServiceName = model.ServiceName,
                EventDate = model.EventDate,
                User = model.User,
                Action = model.Action,
                Resource = model.Resource,
                PropertyChanges = model.PropertyChanges?.Select(p => new EntityPropertyChange { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList(),
                Notes = model.Notes
            };
        }
    }
}
