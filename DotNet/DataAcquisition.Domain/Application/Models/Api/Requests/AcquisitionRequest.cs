namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
public record AcquisitionRequest(string logId, string facilityId, bool ignoreStatusConstraint = false);
