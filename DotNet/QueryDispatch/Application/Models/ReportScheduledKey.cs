using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class ReportScheduledKey
    {
        [DataMember]
        public string FacilityId { get; set; }
        [DataMember]
        public string ReportType { get; set; }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(FacilityId) || string.IsNullOrWhiteSpace(ReportType))
            {
                return false;
            }

            return true;
        }
    }
}
