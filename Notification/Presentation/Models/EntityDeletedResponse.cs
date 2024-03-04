namespace LantanaGroup.Link.Notification.Presentation.Models
{
    public class EntityDeletedResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Id { get; set; } = null!;
        public EntityDeletedResponse() { }

        public EntityDeletedResponse(string message, string id) { Message = message; Id = id; }
    }
}
