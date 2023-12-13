using LantanaGroup.Link.Census.Models.Messages;

namespace LantanaGroup.Link.Census.Models;

public abstract class BaseResponse : IBaseResponse
{
    public string TopicName { get; set; }
    public string FacilityId { get; set; }
    public string CorrelationId { get; set; }
}