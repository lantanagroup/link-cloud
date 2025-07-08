using System.Runtime.Serialization;

namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    [DataContract]
    public class RequestResponse
    {
        [DataMember]
        public string Message { get; set; } = string.Empty;
        [DataMember]
        public string Id { get; set; } = string.Empty;
    }
}
