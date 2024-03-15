using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Entities;
using LantanaGroup.Link.DataAcquisition.Services;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;
using MediatR;
using LantanaGroup.Link.DataAcquisition.Application.Repositories.FhirApi;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Census;

public class GetPatientCensusRequest : IRequest<List<IBaseMessage>>
{
    public string FacilityId { get; set; }
}

public class GetPatientCensusRequestHandler : IRequestHandler<GetPatientCensusRequest, List<IBaseMessage>>
{
    private readonly ILogger<GetPatientCensusRequestHandler> _logger;
    private readonly IAuthenticationRetrievalService _authRetrievalService;
    private readonly IFhirQueryListConfigurationRepository _fhirQueryListConfigurationRepository;
    private readonly IFhirApiRepository _fhirApiRepository;

    public GetPatientCensusRequestHandler(
        ILogger<GetPatientCensusRequestHandler> logger,
        IAuthenticationRetrievalService authRetrievalService,
        IFhirQueryListConfigurationRepository fhirQueryListConfigurationRepository,
        IFhirApiRepository fhirApiRepository
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authRetrievalService = authRetrievalService ?? throw new ArgumentNullException(nameof(authRetrievalService));
        _fhirQueryListConfigurationRepository = fhirQueryListConfigurationRepository ?? throw new ArgumentNullException(nameof(fhirQueryListConfigurationRepository));
        _fhirApiRepository = fhirApiRepository ?? throw new ArgumentNullException(nameof(fhirApiRepository));
    }

    public async Task<List<IBaseMessage>> Handle(GetPatientCensusRequest request, CancellationToken cancellationToken)
    {
        PatientIDsAcquiredMessage result = new PatientIDsAcquiredMessage();
        var facilityConfig = await _fhirQueryListConfigurationRepository.GetByFacilityIdAsync(request.FacilityId);

        if (facilityConfig == null)
        {
            throw new Exception($"Missing census configuration for facility {request.FacilityId}. Unable to proceed with request.");
        }


        (bool isQueryParam, object authHeader) authHeader = (false, null);

        if (facilityConfig.Authentication != null)
        {
            authHeader = await BuildeAuthHeader(facilityConfig.Authentication);
        }
        

        List<List> resultLists = new List<List>();
        foreach(var list in facilityConfig.EHRPatientLists)
        {
           foreach (var listId in list.ListIds)
            {
                try
                {
                    resultLists.Add(await _fhirApiRepository.GetPatientList(facilityConfig.FhirBaseServerUrl, listId, facilityConfig.Authentication));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving patient list id {1} for facility {2}.", listId, facilityConfig.FacilityId);
                    throw new FhirApiFetchFailureException($"Error retrieving patient list id {listId} for facility {facilityConfig.FacilityId}.", ex);
                }
            }
        }

        var finalList = new List();
        resultLists.ForEach(x =>
        {
            finalList.Entry.AddRange(x.Entry);
        });

        result.PatientIds = finalList;

        return new List<IBaseMessage> { result };
    }

    private async Task<(bool isQueryParam, object? authHeader)> BuildeAuthHeader(AuthenticationConfiguration auth)
    {
        (bool isQueryParam, object authHeader) authHeader = (false, null);
        IAuth authService = _authRetrievalService.GetAuthenticationService(auth);

        if(authService == null)
        {
            return (false, null);
        }

        authHeader = await authService.SetAuthentication(auth);
        return authHeader;
    }

    private string BuildRelativeUrl(List<OverrideTenantParameters> parameters, string relativeBasePath)
    {
        //clean up relative base path
        relativeBasePath = relativeBasePath.TrimEnd('/');
        relativeBasePath = relativeBasePath.TrimStart('/');
        relativeBasePath = relativeBasePath.EndsWith('?') ? relativeBasePath : $"{relativeBasePath}?";

        if (parameters == null || !parameters.Any(x => x.IsQuery))
        {
            relativeBasePath = relativeBasePath.Trim('?');
            return relativeBasePath;
        }

        foreach (var parameter in parameters)
        {
            if (parameter.IsQuery)
            {
                relativeBasePath = $"{relativeBasePath}{parameter.Name}={string.Join(',', parameter.Values)}";
            }
        }
        return relativeBasePath;
    }
}
