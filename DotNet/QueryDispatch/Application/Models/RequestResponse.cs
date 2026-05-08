using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class RequestResponse
    {
        [DataMember]
        public string Message { get; set; }
        [DataMember]
        public string Id { get; set; }
    }
}
