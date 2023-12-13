namespace LantanaGroup.Link.Audit.Presentation.Models
{
    public class EntityCreatedResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Id { get; set; }

        public EntityCreatedResponse() { }

        public EntityCreatedResponse(string message, string id) { Message = message; Id = id; }
    }
}
