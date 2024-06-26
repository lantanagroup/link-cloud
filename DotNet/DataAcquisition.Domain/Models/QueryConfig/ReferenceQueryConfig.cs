using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;

[BsonDiscriminator("ReferenceQueryConfig")]
public class ReferenceQueryConfig : IQueryConfig
{
    public string ResourceType { get; set; }
    public OperationType OperationType { get; set; }
    public int Paged { get; set; }

    public ReferenceQueryConfig()
    {

    }
}
