namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class PatientEventValue
    {
        public string PatientId { get; set; }
        public string EventType { get; set; }

        public bool IsValid()
        { 
            if (string.IsNullOrWhiteSpace(PatientId) || string.IsNullOrWhiteSpace(EventType))
            {
                return false;
            }

            return true;
        }
    }
}
