using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using System.Net;

namespace LantanaGroup.Link.Automation.Services;

public class QueryConfigApiClient
{
    private readonly DataAcquisitionServiceClient _dataAcqClient;
    private readonly IAutomationOutput _output;
    private readonly AutomationConfig _config;

    public QueryConfigApiClient(DataAcquisitionServiceClient dataAcqClient, IAutomationOutput output, AutomationConfig config)
    {
        _dataAcqClient = dataAcqClient;
        _output = output;
        _config = config;
    }

    public async Task CreateQueryConfigAsync(string facilityId)
    {
        _output.WriteLine("Creating query config...");
        var body = new CreateFhirQueryConfigurationRequestApiModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = _config.InternalFhirServerBase,
            MaxConcurrentRequests = _config.FhirQuery.MaxConcurrentRequests,
            MaxRetries = 3
        };

        var status = await _dataAcqClient.CreateFhirQueryConfigurationAsync(body);

        if (status == HttpStatusCode.Conflict)
        {
            _output.WriteLine($"Query config for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        if (status != HttpStatusCode.Created)
            _output.WriteLine($"Expected HTTP 201 Created but received {status}");
        AutomationInvariant.Require(status == HttpStatusCode.Created,
            $"Expected HTTP 201 Created but received {status}");
    }

    public async Task CreateQueryPlanAsync(string facilityId, string? measureId, string ehrDescription)
    {
        _output.WriteLine("Creating query plan...");

        await PostQueryPlan(facilityId, measureId, ehrDescription, "Discharge");
        await PostQueryPlan(facilityId, measureId, ehrDescription, "Monthly");
    }

    private async Task PostQueryPlan(string facilityId, string? measureId, string ehrDescription, string type)
    {
        var jBody = QueryPlanBuilder.BuildQueryPlan(facilityId, measureId, ehrDescription, type);
        var body = new CreateQueryPlanRequestApiModel
        {
            PlanName = jBody.Value<string>("PlanName"),
            FacilityId = jBody.Value<string>("FacilityId") ?? facilityId,
            EHRDescription = jBody.Value<string>("EHRDescription") ?? ehrDescription,
            LookBack = jBody.Value<string>("LookBack") ?? "P0D",
            Type = jBody.Value<string>("Type") ?? type,
            InitialQueries = jBody["InitialQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
            SupplementalQueries = jBody["SupplementalQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>()
        };

        var status = await _dataAcqClient.CreateQueryPlanAsync(facilityId, body);

        if (status == HttpStatusCode.Conflict)
        {
            _output.WriteLine($"{type} query plan for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        if (status != HttpStatusCode.Created)
            _output.WriteLine($"Expected HTTP 201 Created for {type} query plan but received {status}");
        AutomationInvariant.Require(status == HttpStatusCode.Created,
            $"Expected HTTP 201 Created for {type} query plan but received {status}");
    }
}
