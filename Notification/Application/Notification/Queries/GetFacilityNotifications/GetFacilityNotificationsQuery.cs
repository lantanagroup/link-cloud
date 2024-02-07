using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

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

        public async Task<PagedNotificationModel> Execute(string facilityId, string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Notifications Query");

            try
            {
                var (result, metadata) = await _datastore.FindAsync(null, facilityId, null, null, null, null, null, sortBy, pageSize, pageNumber);

                //convert AuditEntity to AuditModel
                using (ServiceActivitySource.Instance.StartActivity("Map List Results"))
                {
                    List<NotificationModel> notifications = result.Select(x => new NotificationModel
                    {
                        Id = x.Id,
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
            catch (Exception ex)
            {     
                Activity.Current?.SetStatus(ActivityStatusCode.Error);                
                throw;
            }
        }
    }
}
