using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Services
{
    public interface IPatientCensusService
    {
        Task<PatientIDsAcquiredMessage> Get(string facilityId, CancellationToken cancellationToken);
    }

    public class PatientCensusService : IPatientCensusService
    {
        private readonly ILogger<PatientCensusService> _logger;
        private readonly IAuthenticationRetrievalService _authRetrievalService;
        private readonly IFhirQueryListConfigurationManager _fhirQueryListConfigurationManager;
        private readonly IFhirApiService _fhirApiManager;

        public PatientCensusService(
            ILogger<PatientCensusService> logger,
            IAuthenticationRetrievalService authRetrievalService,
            IFhirQueryListConfigurationManager fhirQueryListConfigurationManager,
            IFhirApiService fhirApiManager
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authRetrievalService = authRetrievalService ?? throw new ArgumentNullException(nameof(authRetrievalService));
            _fhirQueryListConfigurationManager = fhirQueryListConfigurationManager ??
                                                 throw new ArgumentNullException(nameof(fhirQueryListConfigurationManager));
            _fhirApiManager = fhirApiManager ?? throw new ArgumentNullException(nameof(fhirApiManager));
        }

        public async Task<PatientIDsAcquiredMessage> Get(string facilityId, CancellationToken cancellationToken)
        {
            PatientIDsAcquiredMessage result = new PatientIDsAcquiredMessage();
            var facilityConfig = await _fhirQueryListConfigurationManager.GetAsync(facilityId, cancellationToken);

            if (facilityConfig == null)
            {
                throw new Exception(
                    $"Missing census configuration for facility {facilityId}. Unable to proceed with request.");
            }


            (bool? isQueryParam, object? authHeader) authHeader = (false, null);

            if (facilityConfig.Authentication != null)
            {
                authHeader = await BuildeAuthHeader(facilityId, facilityConfig.Authentication);
            }


            List<List> resultLists = new List<List>();
            foreach (var list in facilityConfig.EHRPatientLists)
            {
                foreach (var listId in list.ListIds)
                {
                    try
                    {
                        resultLists.Add(await _fhirApiManager.GetPatientList(facilityConfig.FhirBaseServerUrl, listId, facilityId, facilityConfig.Authentication));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving patient list id {1} for facility {2} with base url of {3}.",
                            listId, facilityConfig.FacilityId, facilityConfig.FhirBaseServerUrl);
                        throw new FhirApiFetchFailureException(
                            $"Error retrieving patient list id {listId} for facility {facilityConfig.FacilityId}.", ex);
                    }
                }
            }

            var finalList = new List();
            resultLists.ForEach(x =>
            {
                finalList.Entry.AddRange(x.Entry);
            });

            result.PatientIds = finalList;

            return result;
        }

        private async Task<(bool isQueryParam, object? authHeader)> BuildeAuthHeader(string facilityId, AuthenticationConfiguration auth)
        {
            (bool isQueryParam, object authHeader) authHeader = (false, null);
            IAuth authService = _authRetrievalService.GetAuthenticationService(auth);

            if (authService == null)
            {
                return (false, null);
            }

            authHeader = await authService.SetAuthentication(facilityId, auth);
            return authHeader;
        }
    }
}
