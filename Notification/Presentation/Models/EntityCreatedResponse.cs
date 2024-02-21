namespace LantanaGroup.Link.Notification.Presentation.Models
{
    public class EntityCreatedResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Id { get; set; } = null!;
        public EntityCreatedResponse() { }

        public EntityCreatedResponse(string message, string id) { Message = message; Id = id; }
    }
}
