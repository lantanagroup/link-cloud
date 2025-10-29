namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class QueryResultsModel
{
    public string PatientId { get; set; }
    public List<QueryResult> QueryResults { get; set; }
}
