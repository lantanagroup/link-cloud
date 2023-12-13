using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public class GetFacilityConfigurationListQuery : IGetFacilityConfigurationListQuery
    {
        private readonly ILogger<GetFacilityConfigurationListQuery> _logger;
        private readonly INotificationConfigurationRepository _datastore;

        public GetFacilityConfigurationListQuery(ILogger<GetFacilityConfigurationListQuery> logger, INotificationConfigurationRepository datastore)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<PagedNotificationConfigurationModel> Execute(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Find Notification Configurations Query");

            try
            {
                var (result, metadata) = await _datastore.FindAsync(searchText, filterFacilityBy, sortBy, pageSize, pageNumber);

                //convert AuditEntity to AuditModel
                List<NotificationConfigurationModel> configs = result.Select(x => new NotificationConfigurationModel
                {
                    Id = x.Id,
                    FacilityId = x.FacilityId,
                    EmailAddresses = x.EmailAddresses,
                    EnabledNotifications = x.EnabledNotifications,
                    Channels = x.Channels
                }).ToList();

                PagedNotificationConfigurationModel pagedNotificationConfigurations = new PagedNotificationConfigurationModel(configs, metadata);

                return pagedNotificationConfigurations;
            }
            catch (NullReferenceException ex)
            {                
                _logger.LogDebug(new EventId(NotificationLoggingIds.ListItems, "Notification Service - List notification configurations"), ex, "Failed to find notification configurations.");
                var queryEx = new ApplicationException("Failed to execute the request to find notification configurations.", ex);
                throw queryEx;                
            }
        }
    }
}
