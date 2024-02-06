using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public class GetAllAuditEventsQuery : IGetAllAuditEventsQuery
    {
        private readonly ILogger<GetAllAuditEventsQuery> _logger;
        private readonly IAuditRepository _datastore;

        public GetAllAuditEventsQuery(ILogger<GetAllAuditEventsQuery> logger, IAuditRepository datastore) 
        { 
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
            _datastore = datastore?? throw new ArgumentNullException(nameof(datastore));
        }

        /// <summary>
        /// Retrieves all audit events stored in the audit service.
        /// </summary>
        /// <returns>A list of Audit events</returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task<List<AuditModel>> Execute()
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get All Audit Events Query");

            var result = await _datastore.GetAllAsync();

            List<AuditModel> auditEvents = result.Select(x => new AuditModel
            {
                Id = x.Id,
                FacilityId = x.FacilityId,
                ServiceName = x.ServiceName,
                EventDate = x.EventDate,
                User = x.User,
                Action = x.Action,
                Resource = x.Resource,
                PropertyChanges = x.PropertyChanges?.Select(p => new PropertyChangeModel { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList(),
                Notes = x.Notes
            }).ToList();

            return auditEvents;
        }
    }
}
