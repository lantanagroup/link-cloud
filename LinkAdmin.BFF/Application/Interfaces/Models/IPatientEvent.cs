namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models
{
    public interface IPatientEvent
    {
        string Key { get; set; }
        string PatientId { get; set; }
        string EventType { get; set; }
    }
}
