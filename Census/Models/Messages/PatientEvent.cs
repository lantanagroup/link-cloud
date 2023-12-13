namespace LantanaGroup.Link.Census.Models.Messages;

public class PatientEvent : IBaseMessage
{
    public string PatientId { get; set; }
    public string EventType { get; set; }
}
