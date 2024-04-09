using LantanaGroup.Link.Notification.Application.Interfaces;

namespace LantanaGroup.Link.Notification.Application.Models
{
    public class PagedNotificationModel : IPagedModel<NotificationModel>
    {
        public List<NotificationModel> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }

        public PagedNotificationModel() { Records = new List<NotificationModel>(); Metadata = new PaginationMetadata(); }

        public PagedNotificationModel(List<NotificationModel> records, PaginationMetadata metadata) 
        { 
            Records = records; 
            Metadata = metadata;
        }
    }
}
