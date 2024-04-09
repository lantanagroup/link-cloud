using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public class GetFacilityNotificationsQuery : IGetFacilityNotificatonsQuery
    {
        private readonly ILogger<GetFacilityNotificationsQuery> _logger;
        private readonly INotificationRepository _datastore;

        public GetFacilityNotificationsQuery(ILogger<GetFacilityNotificationsQuery> logger, INotificationRepository datastore)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<PagedNotificationModel> Execute(string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Notifications Query");

            try
            {
                var (result, metadata) = await _datastore.GetFacilityNotificationsAsync(facilityId, sortBy, sortOrder, pageSize, pageNumber, cancellationToken);

                //convert AuditEntity to AuditModel
                using (ServiceActivitySource.Instance.StartActivity("Map List Results"))
                {
                    List<NotificationModel> notifications = result.Select(x => new NotificationModel
                    {
                        Id = x.Id.Value.ToString(),
                        NotificationType = x.NotificationType,
                        FacilityId = x.FacilityId,
                        CorrelationId = x.CorrelationId,
                        Subject = x.Subject,
                        Body = x.Body,
                        Recipients = x.Recipients,
                        Bcc = x.Bcc,
                        CreatedOn = x.CreatedOn,
                        SentOn = x.SentOn
                    }).ToList();

                    PagedNotificationModel pagedNotifications = new PagedNotificationModel(notifications, metadata);

                    return pagedNotifications;
                }
                
            }
            catch (Exception)
            {     
                Activity.Current?.SetStatus(ActivityStatusCode.Error);                
                throw;
            }
        }
    }
}
