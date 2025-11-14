
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class PatientListMessage
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReportTrackingId { get; set; }
        public List<PatientListItem> PatientLists { get; set; } = new();
    }
}
