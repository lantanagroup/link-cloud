using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.notification
{
    public class PagedNotificationConfigurationModel : IPagedModel<NotificationConfigurationModel>
    {
        public List<NotificationConfigurationModel> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }

        public PagedNotificationConfigurationModel() { }

        public PagedNotificationConfigurationModel(List<NotificationConfigurationModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
