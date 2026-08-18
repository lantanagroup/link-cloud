using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Vendor
{
    Epic,
    Cerner
}
