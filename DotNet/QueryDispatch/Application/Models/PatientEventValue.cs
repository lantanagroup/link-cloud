using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class PatientEventValue
    {
        [DataMember]
        public string PatientId { get; set; }
        [DataMember]
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
