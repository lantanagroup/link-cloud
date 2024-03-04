using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public class GetAuditEventQuery : IGetAuditEventQuery
    {
        private readonly ILogger<GetAuditEventQuery> _logger;
        private readonly IAuditRepository _datastore;

        public GetAuditEventQuery(ILogger<GetAuditEventQuery> logger, IAuditRepository datastore)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        /// <summary>
        /// Retrieves an audit event with the given Id.
        /// </summary>
        /// <param name="id">The id of the audit event</param>
        /// <param name="cancellationToken"></param>
        /// <returns>An audit event with and id equal to the id parameter</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ApplicationException"></exception>
        public async Task<AuditModel> Execute(AuditId id, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Audit Event By Id Query");            
            
            var result = await _datastore.GetAsync(id, true, cancellationToken);
            AuditModel? auditEvent = null;
            if (result != null)
            {
                auditEvent = new AuditModel
                {
                    Id = result.Id,
                    FacilityId = result.FacilityId,
                    ServiceName = result.ServiceName,
                    EventDate = result.EventDate,
                    User = result.User,
                    Action = result.Action,
                    Resource = result.Resource,
                    PropertyChanges = result.PropertyChanges?.Select(p => new PropertyChangeModel { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList(),
                    Notes = result.Notes
                };

                return auditEvent;
            }
            else
            {
                return auditEvent;
            }   
        }
    }
}
