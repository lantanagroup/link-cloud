namespace LantanaGroup.Link.Shared.Application.Models.Kafka
{
    public class SubmitPayloadValue
    {
        public string PayLoadType { get; set; } = string.Empty;
        public Guid? PayLoadId { get; set; }
        public string PayLoadUri { get; set; } = string.Empty;
        public List<string> MeasureIds { get; set; } = new();
    }
}
