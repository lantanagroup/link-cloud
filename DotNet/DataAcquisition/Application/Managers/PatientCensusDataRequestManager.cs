using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Managers
{
    public interface IPatientCensusRequestManager
    {
        Task<IBaseMessage> GetPatientCensusRequestManager(string facilityId, CancellationToken cancellationToken);
    }

    public class PatientCensusRequestManager : IPatientCensusRequestManager
    {
        private readonly ILogger<PatientCensusRequestManager> _logger;
        private readonly IAuthenticationRetrievalService _authRetrievalService;
        private readonly IFhirQueryListConfigurationManager _fhirQueryListConfigurationManager;
        private readonly IFhirApiRepository _fhirApiRepository;

        public PatientCensusRequestManager(
            ILogger<PatientCensusRequestManager> logger,
            IAuthenticationRetrievalService authRetrievalService,
            IFhirQueryListConfigurationManager fhirQueryListConfigurationManager,
            IFhirApiRepository fhirApiRepository
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authRetrievalService = authRetrievalService ?? throw new ArgumentNullException(nameof(authRetrievalService));
            _fhirQueryListConfigurationManager = fhirQueryListConfigurationManager ??
                                                 throw new ArgumentNullException(nameof(fhirQueryListConfigurationManager));
            _fhirApiRepository = fhirApiRepository ?? throw new ArgumentNullException(nameof(fhirApiRepository));
        }

        public async Task<IBaseMessage> GetPatientCensusRequestManager(string facilityId, CancellationToken cancellationToken)
        {
            PatientIDsAcquiredMessage result = new PatientIDsAcquiredMessage();
            var facilityConfig = await _fhirQueryListConfigurationManager.GetAsync(facilityId, cancellationToken);

            if (facilityConfig == null)
            {
                throw new Exception(
                    $"Missing census configuration for facility {facilityId}. Unable to proceed with request.");
            }


            (bool isQueryParam, object authHeader) authHeader = (false, null);

            if (facilityConfig.Authentication != null)
            {
                authHeader = await BuildeAuthHeader(facilityConfig.Authentication);
            }


            List<List> resultLists = new List<List>();
            foreach (var list in facilityConfig.EHRPatientLists)
            {
                foreach (var listId in list.ListIds)
                {
                    try
                    {
                        resultLists.Add(await _fhirApiRepository.GetPatientList(facilityConfig.FhirBaseServerUrl, listId,
                            facilityConfig.Authentication));
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

        private async Task<(bool isQueryParam, object? authHeader)> BuildeAuthHeader(AuthenticationConfiguration auth)
        {
            (bool isQueryParam, object authHeader) authHeader = (false, null);
            IAuth authService = _authRetrievalService.GetAuthenticationService(auth);

            if (authService == null)
            {
                return (false, null);
            }

            authHeader = await authService.SetAuthentication(auth);
            return authHeader;
        }
    }
}
