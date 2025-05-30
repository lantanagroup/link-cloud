using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Domain.Entities;

public class BaseEntity
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Id { get; set; } = Guid.NewGuid().ToString(); 
}
