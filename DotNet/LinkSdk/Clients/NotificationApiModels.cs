namespace LantanaGroup.Link.Sdk.Clients;

public sealed class NotificationMessageApiModel
{
    public string? Id { get; set; }
    public string? NotificationType { get; set; }
    public string? FacilityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public List<string>? Recipients { get; set; }
    public List<string>? Bcc { get; set; }
    public DateTime? CreatedOn { get; set; }
    public List<DateTime>? SentOn { get; set; }
}

public sealed class NotificationPageMetadata
{
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
    public long TotalCount { get; set; }
    public long TotalPages { get; set; }
}

public sealed class PagedNotificationApiModel
{
    public List<NotificationMessageApiModel> Records { get; set; } = [];
    public NotificationPageMetadata? Metadata { get; set; }
}

public sealed class NotificationChannelApiModel
{
    public string? Name { get; set; }
    public bool Enabled { get; set; }
}

public sealed class EnabledNotificationApiModel
{
    public string? NotificationType { get; set; }
    public List<string>? Recipients { get; set; }
}

public sealed class NotificationConfigurationApiModel
{
    public string? Id { get; set; }
    public string? FacilityId { get; set; }
    public List<string>? EmailAddresses { get; set; }
    public List<EnabledNotificationApiModel>? EnabledNotifications { get; set; }
    public List<NotificationChannelApiModel>? Channels { get; set; }
}

public sealed class PagedNotificationConfigurationApiModel
{
    public List<NotificationConfigurationApiModel> Records { get; set; } = [];
    public NotificationPageMetadata? Metadata { get; set; }
}
