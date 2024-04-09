using LantanaGroup.Link.DemoApiGateway.Application.models.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces.testing
{
    public interface IDataAcquisitionRequested
    {
        string Key { get; set; }
        string PatientId { get; set; }
        List<ScheduledReport> reports { get; set; }
    }
}
