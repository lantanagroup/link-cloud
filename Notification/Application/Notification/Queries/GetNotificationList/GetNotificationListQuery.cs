using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.Notification.Queries
{
    public class GetNotificationListQuery : IGetNotificationListQuery
    {
        private readonly ILogger<GetNotificationListQuery> _logger;
        private readonly INotificationRepository _datastore;

        public GetNotificationListQuery(ILogger<GetNotificationListQuery> logger, INotificationRepository datastore)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<PagedNotificationModel> Execute(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Find Notifications Query");

            try
            {
                var (result, metadata) = await _datastore.SearchAsync(searchText, filterFacilityBy, filterNotificationTypeBy, createdOnStart, createdOnEnd, sentOnStart, sentOnEnd, sortBy, sortOrder, pageSize, pageNumber, cancellationToken);

                List<NotificationModel> notifications = new List<NotificationModel>();
                if (result != null)
                {
                    using (ServiceActivitySource.Instance.StartActivity("Map List Results"))
                    {
                        notifications.AddRange(result.Select(x => new NotificationModel
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
                        }).ToList());
                    }
                }
                
                PagedNotificationModel pagedNotifications = new PagedNotificationModel(notifications, metadata);

                return pagedNotifications;
                                
            }
            catch (NullReferenceException)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
