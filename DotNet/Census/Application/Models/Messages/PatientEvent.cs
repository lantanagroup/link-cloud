namespace LantanaGroup.Link.Census.Application.Models.Messages;

public class PatientEvent : IBaseMessage
{
    public string PatientId { get; set; }
    public string EventType { get; set; }
}
