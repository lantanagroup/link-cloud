using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public class GetNotificationQuery : IGetNotificationQuery
    {
        private readonly ILogger<GetNotificationQuery> _logger;
        private readonly INotificationRepository _datastore;
        private readonly INotificationFactory _notificationFactory;

        public GetNotificationQuery(ILogger<GetNotificationQuery> logger, INotificationRepository datastore, INotificationFactory notificationFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
        }

        public async Task<NotificationModel> Execute(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Notification By Id Query");

            if (string.IsNullOrEmpty(id)) { throw new ArgumentNullException(nameof(id)); }

            try
            {
                NotificationModel? notification = null;

                var result = await _datastore.GetAsync(id);

                if (result != null)
                {
                    notification = new NotificationModel();
                    notification = _notificationFactory.NotificationModelCreate(result.Id, result.NotificationType, result.FacilityId, result.CorrelationId, result.Subject, result.Body, result.Recipients, result.Bcc, result.CreatedOn, result.SentOn);
                }

                return notification;
            }
            catch (ArgumentNullException ex)
            {                
                Activity.Current?.SetStatus(ActivityStatusCode.Error, $"Failed to get notification, no id provided.");
                throw;
            }
        }
    }
}
