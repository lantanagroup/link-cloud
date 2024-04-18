using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Validate;

public class ValidateFacilityConnectionQuery : IRequest<FacilityConnectionResult>
{
    public string FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? PatientIdentifier { get; set; }
}

public class ValidateFacilityConnectionQueryHandler : IRequestHandler<ValidateFacilityConnectionQuery, FacilityConnectionResult>
{
    private readonly ILogger<ValidateFacilityConnectionQueryHandler> _logger;
    private readonly IFhirApiRepository _fhirRepo;
    private readonly IFhirQueryConfigurationRepository _fhirQueryRepo;

    public ValidateFacilityConnectionQueryHandler(
        ILogger<ValidateFacilityConnectionQueryHandler> logger, 
        IFhirApiRepository fhirRepo, 
        IFhirQueryConfigurationRepository fhirQueryRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _fhirQueryRepo = fhirQueryRepo ?? throw new ArgumentNullException(nameof(fhirQueryRepo));
    }

    public async Task<FacilityConnectionResult> Handle(ValidateFacilityConnectionQuery request, CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new MissingFacilityIdException();
        }

        if(string.IsNullOrWhiteSpace(request.PatientId) && string.IsNullOrWhiteSpace(request.PatientIdentifier))
        {
            throw new MissingPatientIdOrPatientIdentifierException();
        }

        if (!string.IsNullOrWhiteSpace(request.PatientId) && !request.PatientId.StartsWith("Patient/"))
            request.PatientId = $"Patient/{request.PatientId}";

        FhirQueryConfiguration? fhirConfig;
        try
        {
            fhirConfig = await _fhirQueryRepo.GetAsync(request.FacilityId);
            
            if(fhirConfig == null)
            {
                throw new MissingFacilityConfigurationException("No authentication configuration found for facility.");
            }
        }
        catch(MissingFacilityConfigurationException ex)
        {
            _logger.LogError(ex, "Error getting facility connection information for facility {FacilityId}", request.FacilityId);
            throw;
        } 
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error getting facility connection information for facility {FacilityId}", request.FacilityId);
            throw;
        }

        FacilityConnectionResult? result;
        try
        {
            var fhirResult = await _fhirRepo.GetPatient(
                fhirConfig.FhirServerBaseUrl, 
                string.IsNullOrWhiteSpace(request?.PatientId) ? request?.PatientIdentifier : request?.PatientId,
                string.Empty,
                request.FacilityId,
                fhirConfig?.Authentication, 
                cancellationToken);

            if(fhirResult == null)
            {
                result = new FacilityConnectionResult(true, false, null );
            }
            else
            {
                result = new FacilityConnectionResult(true, true);
            }
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
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error validating facility connection for facility {FacilityId}. Please check your configuration.", request.FacilityId);
            throw;
        }

        return result;
    }
}
