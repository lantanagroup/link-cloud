using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure;
using System.Diagnostics;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

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
        /// <returns>An audit event with and id equal to the id parameter</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ApplicationException"></exception>
        public async Task<AuditModel> Execute(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Audit Event By Id Query");

            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            try
            {
                var result = await _datastore.GetAsync(id);
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
            catch (NullReferenceException ex)
            {
                _logger.LogDebug(new EventId(AuditLoggingIds.GetItem, "Audit Service - Get event by id"), ex, "Failed to get audit event with an id of {id}.", id);
                var queryEx = new ApplicationException("Failed to get audit event", ex);
                queryEx.Data.Add("Id", id);
                throw queryEx;
            }
        }
    }
}
