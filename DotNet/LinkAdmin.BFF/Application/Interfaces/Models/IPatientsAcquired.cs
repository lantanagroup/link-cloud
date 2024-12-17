namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Models
{
    public interface IPatientsAcquired
    {
        string facilityId { get; set; }
        List<string> PatientIds { get; set; }
    }
}
