using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services
{
    public interface IPatientCensusService
    {
        Task<PatientIDsAcquired> Get(string facilityId, CancellationToken cancellationToken);
    }

    public class PatientCensusService : IPatientCensusService
    {
        private readonly ILogger<PatientCensusService> _logger;
        private readonly IAuthenticationRetrievalService _authRetrievalService;
        private readonly IFhirQueryListConfigurationManager _fhirQueryListConfigurationManager;
        private readonly IFhirApiService _fhirApiManager;
        private readonly IReadFhirCommand _readFhirCommand;
        private readonly IFhirQueryConfigurationManager _fhirQueryConfigurationManager;
        private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;

        public PatientCensusService(
            ILogger<PatientCensusService> logger,
            IAuthenticationRetrievalService authRetrievalService,
            IFhirQueryListConfigurationManager fhirQueryListConfigurationManager,
            IFhirApiService fhirApiManager
,
            IReadFhirCommand readFhirCommand,
            IFhirQueryConfigurationManager fhirQueryConfigurationManager,
            IDataAcquisitionLogManager dataAcquisitionLogManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authRetrievalService = authRetrievalService ?? throw new ArgumentNullException(nameof(authRetrievalService));
            _fhirQueryListConfigurationManager = fhirQueryListConfigurationManager ??
                                                 throw new ArgumentNullException(nameof(fhirQueryListConfigurationManager));
            _fhirApiManager = fhirApiManager ?? throw new ArgumentNullException(nameof(fhirApiManager));
            _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
            _fhirQueryConfigurationManager = fhirQueryConfigurationManager ??
                                                 throw new ArgumentNullException(nameof(fhirQueryConfigurationManager));
            _dataAcquisitionLogManager = dataAcquisitionLogManager ??
                                                 throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        }

        public async Task<PatientIDsAcquired> Get(string facilityId, CancellationToken cancellationToken)
        {
            PatientIDsAcquired result = new PatientIDsAcquired();
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

            var fhirQueryConfig = await _fhirQueryConfigurationManager.GetAsync(facilityConfig.FacilityId);

            if (fhirQueryConfig == null)
            {
                throw new Exception(
                    $"Missing FHIR query configuration for facility {facilityId}. Unable to proceed with request.");
            }

            List<List> resultLists = new List<List>();
            foreach (var list in facilityConfig.EHRPatientLists)
            {
                foreach (var listId in list.ListIds)
                {
                    try
                    {
                        var log = new DataAcquisitionLog
                        {
                            FacilityId = facilityId,
                            Status = RequestStatus.Pending,
                            QueryType = FhirQueryType.Read,
                            TimeZone = fhirQueryConfig.TimeZone,
                            ExecutionDate = DateTime.UtcNow,
                            Priority = AcquisitionPriority.Normal,
                            ResourceId = listId,
                            FhirQuery = new List<FhirQuery> {
                                new FhirQuery
                                {
                                    FacilityId = facilityId,
                                    QueryType = FhirQueryType.Read,
                                    ResourceTypes = new List<ResourceType> { ResourceType.List },
                                }
                            },
                            IsCensus = true,

                        };
                        resultLists.Add((List)await _readFhirCommand.ExecuteAsync(
                            new ReadFhirCommandRequest(
                                facilityId,
                                ResourceType.List,
                                listId,
                                facilityConfig.FhirBaseServerUrl,
                                fhirQueryConfig), cancellationToken));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error retrieving patient list id {ListId} for facility {FacilityId} with base url of {BaseUrl}.", listId.Sanitize(), facilityConfig.FacilityId.Sanitize(), facilityConfig.FhirBaseServerUrl.Sanitize());
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
