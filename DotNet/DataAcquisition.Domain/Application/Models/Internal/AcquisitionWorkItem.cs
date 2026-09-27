namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;

public record AcquisitionWorkItem(
    long LogId,
    string FacilityId,
    string? TraceId = null,
    string? SpanId = null,
    bool IsPerformanceMode = false
);