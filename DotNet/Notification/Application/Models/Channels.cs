namespace LantanaGroup.Link.Notification.Application.Models
{
    public class Channels
    {
        public bool IncludeTestMessage { get; set; }
        public string TestMessage { get; set; } = string.Empty;
        public string SubjectTestMessage { get; set; } = string.Empty;        
        public bool Email { get; set; }
        
    }
}
