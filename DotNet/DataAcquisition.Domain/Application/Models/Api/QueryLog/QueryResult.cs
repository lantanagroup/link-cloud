namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class QueryResult
{
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string QueryType { get; set; }
    public bool IsSuccessful { get; set; }  
}
