using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models;


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AdHocType
{
    [StringValue("Manual")]
    Manual = 0,
    [StringValue("Automatic")]
    Automatic = 1
}
