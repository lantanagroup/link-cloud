namespace LantanaGroup.Link.Census.Application.Models;

public abstract class BaseResponse : IBaseResponse
{
    public string TopicName { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;}