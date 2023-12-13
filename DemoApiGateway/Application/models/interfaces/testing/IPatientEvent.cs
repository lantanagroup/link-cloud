namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces.testing
{
    public interface IPatientEvent
    {
        string Key { get; set; }
        string PatientId { get; set; }
        string EventType { get; set; }
    }
}
