using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class ReportScheduledValue
    {
        [DataMember]
        public List<KeyValuePair<string, string>> Parameters { get; set; }

        public bool IsValid()
        {
            if (Parameters == null || Parameters.Count == 0)
            {
                return false;
            }

            return true;
        }
    }
}
