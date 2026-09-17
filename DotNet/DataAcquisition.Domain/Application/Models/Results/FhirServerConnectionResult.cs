namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;

/// <summary>
/// Result model for FHIR server connection validation operations.
/// </summary>
/// <param name="IsConnected">Indicates whether the connection to the FHIR server was successful.</param>
/// <param name="ErrorMessage">A message describing the result of the connection validation, populated when the connection was not successful.</param>
public record FhirServerConnectionResult(bool IsConnected, string? ErrorMessage = null);
