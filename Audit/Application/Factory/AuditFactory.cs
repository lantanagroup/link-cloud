using LantanaGroup.Link.Audit.Application.Commands;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Factory
{
    public class AuditFactory : IAuditFactory
    {
        /// <summary>
        /// Create a new instance of a CreateAuditEventModel 
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="serviceName"></param>
        /// <param name="correlationId"></param>
        /// <param name="eventDate"></param>
        /// <param name="user"></param>
        /// <param name="action"></param>
        /// <param name="resource"></param>
        /// <param name="propertyChanges"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public CreateAuditEventModel Create(string? facilityId, string? serviceName, string? correlationId, DateTime? eventDate, string? userId, string? user, AuditEventType? action, string? resource, List<PropertyChangeModel>? propertyChanges, string? notes)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Factory - Create Audit Event Model");

            CreateAuditEventModel audit = new CreateAuditEventModel();
            audit.FacilityId = facilityId;
            audit.ServiceName = serviceName;
            audit.CorrelationId = correlationId;
            audit.EventDate = eventDate;
            audit.UserId = userId;
            audit.User = user;
            audit.Action = nameof(action);
            audit.Resource = resource;


            if (propertyChanges is not null)
            {
                if (audit.PropertyChanges is not null)
                    audit.PropertyChanges.Clear();
                else
                    audit.PropertyChanges = new List<PropertyChangeModel>();

                audit.PropertyChanges.AddRange(propertyChanges);
            }

            audit.Notes = notes;

            return audit;
        }

        /// <summary>
        /// Create a new instance of an AuditEntity 
        /// </summary>
        /// <param name="facilityId"></param>
        /// <param name="serviceName"></param>
        /// <param name="correlationId"></param>
        /// <param name="eventDate"></param>
        /// <param name="user"></param>
        /// <param name="action"></param>
        /// <param name="resource"></param>
        /// <param name="propertyChanges"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public AuditEntity Create(string? facilityId, string? serviceName, string? correlationId, DateTime? eventDate, string? userId, string? user, string? action, string? resource, List<PropertyChangeModel>? propertyChanges, string? notes)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Factory - Create Audit Entity");

            AuditEntity audit = new AuditEntity();
            audit.Id = Guid.NewGuid().ToString();
            audit.FacilityId = facilityId;
            audit.ServiceName = serviceName;
            audit.CorrelationId = correlationId;
            audit.EventDate = eventDate;
            audit.UserId = userId;
            audit.User = user;
            audit.Action = action;
            audit.Resource = resource;
            audit.CreatedOn = DateTime.UtcNow;


            if (propertyChanges is not null)
            {
                if (audit.PropertyChanges is not null)
                    audit.PropertyChanges.Clear();
                else
                    audit.PropertyChanges = new List<EntityPropertyChange>();

                audit.PropertyChanges.AddRange(propertyChanges.Select(p => new EntityPropertyChange { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList());
            }

            audit.Notes = notes;

            return audit;
        }

    }
}
