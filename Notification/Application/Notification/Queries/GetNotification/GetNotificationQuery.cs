using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;

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

        public async Task<NotificationModel> Execute(NotificationId id, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Notification By Id Query");           

            try
            {                
                var notification = await _datastore.GetAsync(id, true, cancellationToken);

                if (notification is null)
                {
                    return null;
                }
                
                var model = _notificationFactory.NotificationModelCreate(notification.Id, notification.NotificationType, notification.FacilityId, notification.CorrelationId, notification.Subject, notification.Body, notification.Recipients, notification.Bcc, notification.CreatedOn, notification.SentOn);

                return model;
            }
            catch (ArgumentNullException)
            {                
                Activity.Current?.SetStatus(ActivityStatusCode.Error, $"Failed to get notification, no id provided.");
                throw;
            }
        }
    }
}
