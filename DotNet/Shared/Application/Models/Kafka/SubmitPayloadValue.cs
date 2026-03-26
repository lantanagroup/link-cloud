using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class SubmitPayloadValue
    {
        public required PayloadType PayloadType { get; set; }       
        public required string? PayloadUri { get; set; }
        public required List<string> ReportTypes { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? PatientId { get; set; }
    }
}
