namespace LantanaGroup.Link.DemoApiGateway.Application.models.notification;

public class PagedNotificationModel
{
    public List<NotificationModel> Records { get; set; }
    public PaginationMetadata Metadata { get; set; }

    public PagedNotificationModel() { }

    public PagedNotificationModel(List<NotificationModel> records, PaginationMetadata metadata)
    {
        Records = records;
        Metadata = metadata;
    }
}
