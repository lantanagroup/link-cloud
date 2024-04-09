using LantanaGroup.Link.Notification.Infrastructure.Logging;

namespace LantanaGroup.Link.Notification.Application.Models
{
    public record NotificationConfigurationSearchRecord
    {
        [PiiData]
        public string? SearchText { get; init; }
        public string? FilterFacilityBy { get; init; }      
        public string? SortBy { get; init; }
        public int PageSize { get; init; }
        public int PageNumber { get; init; }
    }
}
