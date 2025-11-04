using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;

public record FacilityConnectionResult(bool IsConnected, bool IsPatientFound, string? ErrorMessage = null, List<Resource>? results = null);
