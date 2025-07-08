namespace LantanaGroup.Link.Census.Application.Models.Messages;

public class PatientEvent : IBaseMessage
{
    public string PatientId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
}
