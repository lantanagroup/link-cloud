using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.testing
{
    public class PatientEvent : IPatientEvent
    {
        public string Key { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
    }

    public class PatientEventMessage
    {
        public string PatientId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
    }
}
