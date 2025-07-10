using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class SubmitPayloadValue
    {
        public required PayloadType PayloadType { get; set; }
        public required string PayLoadId { get; set; }
        public required string PayLoadUri { get; set; }
        public required List<string> MeasureIds { get; set; }
    }
}
