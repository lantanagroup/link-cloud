namespace LantanaGroup.Link.Notification.Presentation.Models
{
    public class EntityUpdateddResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Id { get; set; } = null!;
        public EntityUpdateddResponse() { }

        public EntityUpdateddResponse(string message, string id) { Message = message; Id = id; }
    }
}
