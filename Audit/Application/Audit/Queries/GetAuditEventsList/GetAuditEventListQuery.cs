using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public class GetAuditEventListQuery : IGetAuditEventListQuery
    {
        private readonly ILogger<GetAuditEventListQuery> _logger;
        private readonly IAuditRepository _datastore;

        public GetAuditEventListQuery(ILogger<GetAuditEventListQuery> logger, IAuditRepository datastore) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));

        }

        /// <summary>
        /// Returns a list of audit events based on one or more filtering optoins.
        /// </summary>
        /// <param name="searchText">Full text search</param>
        /// <param name="filterFacilityBy">Return only events that deal with a facility</param>
        /// <param name="filterCorrelationBy">Return only events that have a specified correlation id</param>
        /// <param name="filterServiceBy">Return only events that took place in a specified servicve</param>
        /// <param name="filterActionBy">Return only events that were caused by a specified action</param>
        /// <param name="filterUserBy">Return only events that were caused by a specified user</param>
        /// <param name="sortBy">Sort events by the specified property</param>
        /// <param name="pageSize">The number of events to return per page</param>
        /// <param name="pageNumber">The page of event to return</param>
        /// <returns>A paged list of audit events</returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task<PagedAuditModel> Execute(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("List Audit Event Query");

            var (result, metadata) = await _datastore.FindAsync(searchText, filterFacilityBy, filterCorrelationBy, filterServiceBy, filterActionBy, filterUserBy, sortBy, pageSize, pageNumber);

            //convert AuditEntity to AuditModel
            using (ServiceActivitySource.Instance.StartActivity("Map List Results"))
            {

                List<AuditModel> auditEvents = result.Select(x => new AuditModel
                {
                    Id = x.Id,
                    FacilityId = x.FacilityId,
                    CorrelationId = x.CorrelationId,
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
}
