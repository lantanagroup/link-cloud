namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public record FacilityConnectionResult(bool IsConnected, bool IsPatientFound, string? ErrorMessage = null);
