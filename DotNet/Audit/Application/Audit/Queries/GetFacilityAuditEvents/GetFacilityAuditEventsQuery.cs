using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public class GetFacilityAuditEventsQuery : IGetFacilityAuditEventsQuery
    {
        private readonly ILogger<GetFacilityAuditEventsQuery> _logger;
        private readonly IAuditRepository _datastore;

        public GetFacilityAuditEventsQuery(ILogger<GetFacilityAuditEventsQuery> logger, IAuditRepository datastore) 
        { 
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
            _datastore = datastore?? throw new ArgumentNullException(nameof(datastore));
        }

        /// <summary>
        /// Retrieves all audit events stored in the audit service.
        /// </summary>
        /// <returns>A list of Audit events</returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task<PagedAuditModel> Execute(string facilityId, string? sortBy, SortOrder? sortOrder, int pageNumber, int PageSize, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("Get All Audit Events Query",
            [
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.SortBy, sortBy),
                new KeyValuePair<string, object?>(DiagnosticNames.SortOrder, sortOrder),
                new KeyValuePair<string, object?>(DiagnosticNames.PageSize, PageSize),
                new KeyValuePair<string, object?>(DiagnosticNames.PageNumber, pageNumber)
            ]);

            var (result, metadata) = await _datastore.GetByFacilityAsync(facilityId, sortBy, sortOrder, PageSize, pageNumber, cancellationToken);

            List<AuditModel> auditEvents = result.Select(x => new AuditModel
            {
                Id = x.AuditId.Value.ToString(),
                FacilityId = x.FacilityId,
                ServiceName = x.ServiceName,
                EventDate = x.EventDate,
                User = x.User,
                Action = x.Action,
                Resource = x.Resource,
                PropertyChanges = x.PropertyChanges?.Select(p => new PropertyChangeModel { PropertyName = p.PropertyName, InitialPropertyValue = p.InitialPropertyValue, NewPropertyValue = p.NewPropertyValue }).ToList(),
                Notes = x.Notes
            }).ToList();

            PagedAuditModel pagedAuditEvents = new PagedAuditModel(auditEvents, metadata);

            return pagedAuditEvents;
        }
    }
}
