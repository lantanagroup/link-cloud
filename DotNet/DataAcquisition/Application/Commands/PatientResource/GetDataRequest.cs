using LantanaGroup.Link.DataAcquisition.Entities;
using LantanaGroup.Link.DataAcquisition.Application.Services.Auth;
using MediatR;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.DataAcquisition.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Services;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Builders;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;

public class GetDataRequest : IRequest<DataAcquiredMessage>
{
    public DataAcquisitionScheduledMessage Message { get; set; }
    public TenantDataAcquisitionConfigModel TenantDataAcquisitionConfigModel { get; set; }
    public string FacilityId { get; set; }
}

public class GetDataRequestHandler : IRequestHandler<GetDataRequest, DataAcquiredMessage>
{
    private readonly ILogger<GetDataRequestHandler> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConfigModelValidator _validator;
    private readonly AuthenticationRetrievalService _authRetrievalService;

    public GetDataRequestHandler(ILogger<GetDataRequestHandler> logger, DataAcqTenantConfigMongoRepo tenantConfigMongoRepo, HttpClient httpClient, AuthenticationRetrievalService authRetrievalService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _validator = new ConfigModelValidator();
        _authRetrievalService = authRetrievalService ?? throw new ArgumentNullException(nameof(authRetrievalService));
    }

    public async Task<DataAcquiredMessage> Handle(GetDataRequest request, CancellationToken cancellationToken)
    {
        List<(string fullUrl, object resource)> results = null;

        if (string.IsNullOrWhiteSpace(request.Message.TenantId))
        {
            var message = $"Tenant ID is missing. Unable to process message: {request.Message}";
            _logger.LogWarning(message);
            throw new Exception(message);
        }

        var configModelValidationResults = _validator.ValidateConfigModel(request.TenantDataAcquisitionConfigModel);

        if (!configModelValidationResults.IsSuccess)
        {
            //do Some error handling here
            return null;
        }

        var facilityConfigValidationResults = _validator.ValidateFacility(request.TenantDataAcquisitionConfigModel, request.FacilityId);
        if (!facilityConfigValidationResults.IsSuccess)
        {
            //do Some error handling here
            return null;
        }

        //get facility config
        var facilityConfig = request.TenantDataAcquisitionConfigModel.Facilities.FirstOrDefault(x => x.FacilityId == request.FacilityId);

        var resourcesToPull = facilityConfig.ResourceSettings.Where(x => request.Message.Type.Exists(y => y.ToString().ToLower() == x.ResourceType?.FirstOrDefault()?.ToLower()));

        (bool isQueryParam, object authHeader) authHeader = (false, null);
        if (facilityConfig.Auth != null)
        {
            IAuth authService = _authRetrievalService.GetAuthenticationService(facilityConfig.Auth);
            authHeader = await authService.SetAuthentication(facilityConfig.Auth);
        }

        foreach (var resource in resourcesToPull)
        {
            //clear request authorization header
            _httpClient.DefaultRequestHeaders.Authorization = null;

            if (resource.ConfigType.ToLower() == ConfigType.USCore.ToString().ToLower())
            {
                (bool isQueryParam, object authHeader) overrideAuthHeader = (false, null);
                if (!resource.UseBaseAuth)
                {
                    IAuth authService = _authRetrievalService.GetAuthenticationService(resource.Auth);
                    overrideAuthHeader = await authService.SetAuthentication(resource.Auth);
                }

                //get fhir base url
                var fhirBaseUrl = resource.UsCore.UseBaseFhirEndpoint ? facilityConfig.BaseFhirUrl : resource.UsCore.BaseFhirUrl;
                fhirBaseUrl = fhirBaseUrl.EndsWith('/') ? fhirBaseUrl : $"{fhirBaseUrl}/";

                var relativePath = resource.UsCore.UseDefaultRelativeFhirPath ? "Group" : resource.UsCore.RelativeFhirPath;

                relativePath = BuildRelativeUrl(resource.UsCore.Parameters, relativePath);

                var successfulHttpRequest = false;
                var retries = 0;

                try
                {
                    var fullUrl = $"{fhirBaseUrl}{relativePath}";
                    var httpResults = await _httpClient.GetAsync(fullUrl);
                    var jsonStr = await httpResults.Content.ReadAsStringAsync();
                    results.Add((fullUrl, JsonConvert.DeserializeObject(jsonStr)));
                }
                catch (Exception ex)
                {
                    _logger.LogCritical($"Failed GET for baseUrl: {fhirBaseUrl} and relativePath: {relativePath}");
                    _logger.LogError(ex, ex.Message);
                    //will need a failed DataAcquisitionEvent here
                }
            }
        }

        /*
         * OLD CODE *
        //1. get fhir base url
        //2. get group id
        //3. call endpoint for group

        

        //var fhirBaseUrl = request.TenantDataAcquisitionConfigModel.FhirBaseUrl.EndsWith('/') ? request.TenantDataAcquisitionConfigModel.FhirBaseUrl : $"{request.TenantDataAcquisitionConfigModel.FhirBaseUrl}/";
        //configure fhir client
        //var fhirClient = new FhirClient(fhirBaseUrl, _httpClient, new FhirClientSettings
        //{
        //    PreferredFormat = ResourceFormat.Json,
        //    VerifyFhirVersion = false,// avoids calling /metadata on every request
        //    PreferredParameterHandling = SearchParameterHandling.Lenient
        //});

        //var result = await fhirClient.ReadAsync<Group>($"{fhirBaseUrl}Group/{request.TenantDataAcquisitionConfigModel.GroupId}");
        */

        var bundle = new BundleBuilder(results).Build();

        var dataAcquiredMessage = new DataAcquiredMessage
        {
            CorrelationId = request.Message.CorrelationId,
            ReportMonth = request.Message.ReportMonth,
            ReportYear = request.Message.ReportYear,
            TenantId = request.Message.TenantId,
            Type = request.Message.Type,
            Data = bundle
        };

        return dataAcquiredMessage;
    }

    private string BuildRelativeUrl(List<OverrideTenantParameters> parameters, string relativeBasePath)
    {
        //clean up relative base path
        relativeBasePath = relativeBasePath.TrimEnd('/');
        relativeBasePath = relativeBasePath.EndsWith('?') ? relativeBasePath : $"{relativeBasePath}?";

        if (!parameters.Any(x => x.IsQuery))
        {
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