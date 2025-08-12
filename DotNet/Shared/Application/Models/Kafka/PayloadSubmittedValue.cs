using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public class PayloadSubmittedValue
{
    public required PayloadType PayloadType { get; set; }
    public string? PatientId { get; set; }
}