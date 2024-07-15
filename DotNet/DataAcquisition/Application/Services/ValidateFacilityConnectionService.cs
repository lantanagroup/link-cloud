using Confluent.Kafka;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Services
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
        private readonly IMediator _mediator;
        private readonly IFhirApiService _fhirApiService;
        private readonly IFhirQueryConfigurationManager _fhirQueryConfigurationManager;

        public ValidateFacilityConnectionService(
            ILogger<ValidateFacilityConnectionService> logger,
            IFhirApiService fhirApiService,
            IFhirQueryConfigurationManager fhirQueryConfigurationManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fhirApiService = fhirApiService ?? throw new ArgumentNullException(nameof(fhirApiService));
            _fhirQueryConfigurationManager = fhirQueryConfigurationManager ?? throw new ArgumentNullException(nameof(fhirQueryConfigurationManager));
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
                var authenticationConfig = await _fhirQueryConfigurationManager.GetAuthenticationConfigurationByFacilityId(request.FacilityId, cancellationToken);
                var queryConfig = await _fhirQueryConfigurationManager.GetAsync(request.FacilityId, cancellationToken);
                var patient = await _fhirApiService.GetPatient(queryConfig.FhirServerBaseUrl, request.PatientId, Guid.NewGuid().ToString(), request.FacilityId, authenticationConfig, cancellationToken);

                if(patient != null)
                    return new FacilityConnectionResult(true, false);
                else
                    return new FacilityConnectionResult(true, true, patient: patient);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating facility connection for facility {FacilityId}. Please check your configuration.", request.FacilityId);
                throw;
            }
        }
    }

}
