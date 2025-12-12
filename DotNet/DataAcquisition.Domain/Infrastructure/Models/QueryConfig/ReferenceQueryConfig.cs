using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;


public class ReferenceQueryConfig : IQueryConfig
{
    public QueryConfigType QueryConfigType { get; set; } = QueryConfigType.Reference;
    public string ResourceType { get; set; }
    public OperationType OperationType { get; set; } = OperationType.Search;
    public int Paged { get; set; }

    public ReferenceQueryConfig()
    {

    }
}
