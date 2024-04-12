namespace LantanaGroup.Link.PatientsToQuery.Application.Models
{
    public class PatientStatus
    {
        public string PatientId { get; set; }
        public Status Status { get; set; }
    }

    public enum Status { 
        Admit,
        Queried
    }
}
