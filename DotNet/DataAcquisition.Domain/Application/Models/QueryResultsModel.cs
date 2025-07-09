namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class QueryResultsModel
{
    public string PatientId { get; set; } = string.Empty;
    public List<QueryResult> QueryResults { get; set; } = new List<QueryResult>();
}
