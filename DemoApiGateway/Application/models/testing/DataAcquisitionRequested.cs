using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.testing
{
    public class DataAcquisitionRequested : IDataAcquisitionRequested
    {
        public string Key { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public List<ScheduledReport> reports { get; set; } = new List<ScheduledReport>();
    }

    public class DataAcquisitionRequestedMessage
    {
        public string PatientId { get; set; } = string.Empty;
        public List<ScheduledReport> reports { get; set; } = new List<ScheduledReport>();
    }
}
