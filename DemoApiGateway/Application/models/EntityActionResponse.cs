namespace LantanaGroup.Link.DemoApiGateway.Application.models
{
    public class EntityActionResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Id { get; set; }
        public EntityActionResponse() { }

        public EntityActionResponse(string message, string id) { Message = message; Id = id; }
    }
}
