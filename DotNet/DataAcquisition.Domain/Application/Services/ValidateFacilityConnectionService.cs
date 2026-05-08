using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services
{
    public class ValidateFacilityConnectionRequest
    {
        public string? FacilityId { get; set; }
        public string? PatientId { get; set; }
        public string? PatientIdentifier { get; set; }
        public string? MeasureId { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    public interface IValidateFacilityConnectionService
    {
        Task<FacilityConnectionResult> ValidateConnection(ValidateFacilityConnectionRequest request,
            CancellationToken cancellationToken);
    }

    public class ValidateFacilityConnectionService : IValidateFacilityConnectionService
    {
        private readonly ILogger<ValidateFacilityConnectionService> _logger;
        private readonly IPatientDataService _patientDataService;

        public ValidateFacilityConnectionService(ILogger<ValidateFacilityConnectionService> logger, IPatientDataService patientDataService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _patientDataService = patientDataService ?? throw new ArgumentNullException(nameof(patientDataService));
        }

        public async Task<FacilityConnectionResult> ValidateConnection(ValidateFacilityConnectionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.FacilityId))
            {
                throw new MissingFacilityIdException();
            }

            if (string.IsNullOrWhiteSpace(request.PatientId) && string.IsNullOrWhiteSpace(request.PatientIdentifier))
            {
                throw new MissingPatientIdOrPatientIdentifierException();
            }

            if (string.IsNullOrWhiteSpace(request.MeasureId))
            {
                throw new BadRequestException("Missing Measure ID.");
            }

            if (request.Start == null || request.Start == default)
            {
                throw new BadRequestException("Invalid start date.");
            }

            if (request.End == null || request.End == default)
            {
                throw new BadRequestException("Invalid end date.");
            }

            if (!string.IsNullOrWhiteSpace(request.PatientId) && !request.PatientId.StartsWith("Patient/"))
                request.PatientId = $"Patient/{request.PatientId}";

            try
            {
                var results = await _patientDataService.ValidateFacilityConnection(
                    new GetPatientDataRequest
                    {
                        FacilityId = request.FacilityId,
                        CorrelationId = Guid.NewGuid().ToString(),
                        ConsumeResult = new Confluent.Kafka.ConsumeResult<string, Models.Kafka.DataAcquisitionRequested>
                        {
                            Message = new Confluent.Kafka.Message<string, Models.Kafka.DataAcquisitionRequested>
                            {
                                Value = new Models.Kafka.DataAcquisitionRequested
                                {
                                    PatientId = request.PatientId,
                                    QueryType = QueryPlanType.Initial.ToString(),
                                    ScheduledReports = new List<ScheduledReport>
                                    {
                                        new ScheduledReport
                                        {
                                            ReportTypes = new List<string> { request.MeasureId },
                                            StartDate = request.Start.Value,
                                            EndDate = request.End.Value
                                        }
                                    }
                                },
                                Key = request.FacilityId,
                                Headers = new Confluent.Kafka.Headers()
                            }
                        }
                    });

                if (results == null || results.Count == 0)
                {
                    return new FacilityConnectionResult(false, false, "Patient not found for facility.");
                }

                return new FacilityConnectionResult(true, true, "Connection successful.", results);
            }
            catch (Exception ex) when (
                ex is FhirConnectionFailedException ||
                ex is FhirOperationException ||
                ex is StructuralTypeException ||
                ex is HttpRequestException)
            {
                _logger.LogError(ex, "Error validating facility connection for facility {FacilityId}", request.FacilityId);
                throw new FhirConnectionFailedException($"Error validating facility connection for facility {request.FacilityId}", ex);
            }
            catch (MissingFacilityConfigurationException ex)
            {
                _logger.LogError(ex, "No configuration found for Facility ID {facilityId}.", request.FacilityId);
                throw new FhirConnectionFailedException($"No configuration found for Facility ID {request.FacilityId}", ex);
            }
            catch (NotFoundException ex)
            {
                _logger.LogError(ex, "Patient not found for facility {FacilityId}", request.FacilityId);
                throw new FhirConnectionFailedException($"Patient {request.PatientId} not found for facility {request.FacilityId}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating facility connection for facility {FacilityId}. Please check your configuration.", request.FacilityId);
                throw;
            }
        }
    }

}
