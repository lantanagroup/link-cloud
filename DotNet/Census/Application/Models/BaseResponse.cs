namespace LantanaGroup.Link.Census.Application.Models;

public abstract class BaseResponse : IBaseResponse
{
    public string TopicName { get; set; }
    public string FacilityId { get; set; }
    public string CorrelationId { get; set; }
}