using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Domain.Entities;

public class BaseEntity
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Id { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}
