namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
public record AcquisitionRequest(long logId, string facilityId, bool ignoreStatusConstraint = false);
