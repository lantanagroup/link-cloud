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
        /// <param name="searchFilter">Search filter parameters</param>
        /// <returns>A paged list of audit events</returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task<PagedAuditModel> Execute(AuditSearchFilterRecord searchFilter, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("List Audit Event Query");

            var (result, metadata) = await _datastore.SearchAsync(searchFilter.SearchText, searchFilter.FilterFacilityBy, searchFilter.FilterCorrelationBy, 
                searchFilter.FilterServiceBy, searchFilter.FilterActionBy, searchFilter.FilterUserBy, searchFilter.SortBy, searchFilter.SortOrder, 
                searchFilter.PageSize, searchFilter.PageNumber, cancellationToken);

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
