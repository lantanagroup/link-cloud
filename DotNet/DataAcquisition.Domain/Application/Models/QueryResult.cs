namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class QueryResult
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string QueryType { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }  
}
