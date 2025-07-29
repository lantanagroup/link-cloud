namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health
{
    public class HealthResponse
    {
        public string Status { get; set; }
        public Dictionary<string, ComponentStatus> Components { get; set; } = new();
    }

    public class ComponentStatus
    {
        public string Status { get; set; }
        public Dictionary<string, string> Details { get; set; } = new();
    }
}
