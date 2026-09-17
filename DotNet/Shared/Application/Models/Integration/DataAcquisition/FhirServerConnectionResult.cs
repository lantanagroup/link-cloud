namespace LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

/// <summary>
/// Wire model for GET api/data/connectionValidation/$validate. It mirrors the Data Acquisition
/// model of the same name (LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results.FhirServerConnectionResult)
/// by hand, so keep the two in step.
/// </summary>
/// <param name="IsConnected">Indicates whether the connection to the FHIR server was successful.</param>
/// <param name="ErrorMessage">A message describing the result of the connection validation, populated when the connection was not successful.</param>
public record FhirServerConnectionResult(bool IsConnected, string? ErrorMessage = null);
