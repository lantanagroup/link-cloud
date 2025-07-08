using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class PatientEventValue
    {
        [DataMember]
        public string PatientId { get; set; } = string.Empty;
        [DataMember]
        public string EventType { get; set; } = string.Empty;

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
