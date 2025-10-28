using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
public class PatientListModel
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ListType ListType { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TimeFrame TimeFrame { get; set; }
    public List<string> PatientIds { get; set; } = new List<string>();
}
