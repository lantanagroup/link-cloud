namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
public record AcquisitionRequest(string logId, string facilityId, bool ignoreStatusConstraint = false);
