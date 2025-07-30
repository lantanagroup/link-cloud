using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Report.Application.Models;

public class PatientListItem
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ListType ListType { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TimeFrame TimeFrame { get; set; }
    public List<string> PatientIds { get; set; } = new List<string>();
}

