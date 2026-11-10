using System.Linq.Expressions;
using System.Reflection;
using LinqExpr = System.Linq.Expressions.Expression;
using Automation.UI.Models;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;
using Moq;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.DataAcquisitionSteps;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class DataAcquisitionTestSuiteTests
{
    private const int CreatedConfigId = 42;

    public static IEnumerable<object[]> NewChecks()
    {
        foreach (var check in Checks)
            yield return new object[] { check.Name, check.Status, check.Verb };
    }

    [Theory]
    [MemberData(nameof(NewChecks))]
    public void Definitions_name_the_method_and_route(string name, int status, string verb)
    {
        var definition = new DataAcquisitionTestSuite(
                new Mock<IDataAcquisitionServiceClient>().Object,
                new Mock<IFacilityServiceClient>().Object,
                new Mock<IApiHealthSeedContextAccessor>().Object,
                Options.Create(new AutomationConfig()),
                new Mock<IOrganizationResourceMapTemplateStore>().Object,
                new Mock<IHttpClientFactory>().Object,
                Options.Create(new ServiceRegistry()))
            .GetEndpointDefinitions()
            .Single(item => item.EndpointName == name);

        definition.GroupName.Should().StartWith(verb + " ");
        definition.GroupName.Should().Contain("/api/data-acquisition");
        definition.Description.Should().NotBeNullOrWhiteSpace();
        status.Should().BeOneOf(200, 201, 202, 400, 404);
    }

    [Fact]
    public void Definitions_keep_the_existing_steps_ahead_of_the_new_ones()
    {
        var names = CreateSuite(out _).GetEndpointDefinitions().Select(item => item.EndpointName).ToList();

        names.IndexOf(StepNames.Search400BadSortBy).Should().BePositive();
        names.IndexOf(StepNames.Search400BadSortBy).Should().BeLessThan(names.IndexOf(StepNames.FhirConfigPut202));
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ExecuteAsync_passes_every_new_check_on_throwaway_data()
    {
        var suite = CreateSuite(out var client, createdConfigId: CreatedConfigId);

        var results = await suite.ExecuteAsync();

        foreach (var check in Checks)
        {
            var result = results.Should().ContainSingle(item => item.EndpointName == check.Name).Subject;
            result.Skipped.Should().BeFalse();
            result.Passed.Should().BeTrue(result.ErrorMessage);
            result.ExpectedStatusCode.Should().Be(check.Status);
        }

        client.Calls(nameof(IDataAcquisitionServiceClient.UpdateAcquisitionLogAsync))
            .Should().ContainSingle().Which.Args[0].Should().Be(0L);
        client.Calls(nameof(IDataAcquisitionServiceClient.GetSftpConfigurationByIdAsync))
            .Should().ContainSingle().Which.Args[0].Should().Be(Guid.Empty);
        client.Calls(nameof(IDataAcquisitionServiceClient.DeleteSftpConfigurationAsync))
            .Should().ContainSingle().Which.Args[1].Should().Be("not-a-guid");
        client.Calls(nameof(IDataAcquisitionServiceClient.ValidateFhirServerConnectionAsync))
            .Should().ContainSingle().Which.Args[0].Should().Be("not-a-url");
        client.Calls(nameof(IDataAcquisitionServiceClient.DeleteOrganizationLocationConfigurationByIdAsync))
            .Should().ContainSingle().Which.Args[0].Should().Be(CreatedConfigId);

        var facilityValidate = client.Calls(nameof(IDataAcquisitionServiceClient.ValidateFacilityConnectionAsync)).Single();
        facilityValidate.Args[1].Should().BeNull();
        facilityValidate.Args[2].Should().BeNull();
        facilityValidate.Args[3].Should().BeNull();
        facilityValidate.Args[4].Should().BeNull();
        facilityValidate.Args[5].Should().BeNull();

        var adHoc = client.Calls(nameof(IDataAcquisitionServiceClient.TestSftpConnectionAsync)).Single().Args[0]
            .Should().BeOfType<SftpTestConnectionRequestApiModel>().Subject;
        adHoc.HostName.Should().BeEmpty();
        adHoc.Password.Should().BeEmpty();

        foreach (var call in client.Calls(nameof(IDataAcquisitionServiceClient.UpdateOrganizationLocationConfigurationAsync))
                     .Concat(client.Calls(nameof(IDataAcquisitionServiceClient.UpdateOrganizationLocationConfigurationByIdAsync))))
        {
            IsInactive(call.Args.First(arg => arg?.GetType().GetProperty("IsActive") is not null)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_skips_by_id_organization_location_checks_when_create_returns_no_id()
    {
        var suite = CreateSuite(out _, createdConfigId: 0);
        var results = await suite.ExecuteAsync();

        foreach (var name in new[]
                 {
                     StepNames.OrgLocationConfigGet200ById,
                     StepNames.OrgLocationConfigPut202ById,
                     StepNames.OrgLocationConfigDelete202ById
                 })
        {
            results.Should().ContainSingle(item => item.EndpointName == name).Which.Skipped.Should().BeTrue();
        }

        results.Should().ContainSingle(item => item.EndpointName == StepNames.OrgLocationConfigDelete202)
            .Which.Passed.Should().BeTrue();
    }

    private static DataAcquisitionTestSuite CreateSuite(out RecordingDataAcquisitionClient client, int createdConfigId = CreatedConfigId)
    {
        var proxy = DispatchProxy.Create<IDataAcquisitionServiceClient, RecordingDataAcquisitionClient>();
        client = (RecordingDataAcquisitionClient)(object)proxy;
        client.ReturnedConfigId = createdConfigId;

        var facilities = new Mock<IFacilityServiceClient>();
        facilities
            .Setup(item => item.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<FacilityModel> { StatusCode = 201 });
        facilities
            .Setup(item => item.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse { StatusCode = 200 });

        var templates = new Mock<IOrganizationResourceMapTemplateStore>();
        templates
            .Setup(item => item.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationResourceMapTemplate
            {
                Description = "ApiHealth",
                Conditions = [new OrganizationResourceMapCondition { FhirPath = "Location.id.exists()", Priority = 1 }]
            });

        return new DataAcquisitionTestSuite(
            proxy,
            facilities.Object,
            new Mock<IApiHealthSeedContextAccessor>().Object,
            Options.Create(new AutomationConfig()),
            templates.Object,
            new Mock<IHttpClientFactory>().Object,
            Options.Create(new ServiceRegistry()));
    }

    private static bool IsInactive(object? body) =>
        body?.GetType().GetProperty("IsActive")?.GetValue(body) is false;

    private static readonly (string Name, int Status, string Verb)[] Checks =
    [
        (StepNames.FhirConfigPut202, 202, "PUT"),
        (StepNames.FhirListConfigPut200, 200, "PUT"),
        (StepNames.QueryPlanPut202, 202, "PUT"),
        (StepNames.QueryPlanDeleteAll202, 202, "DELETE"),
        (StepNames.AuthGet404, 404, "GET"),
        (StepNames.AuthPost400, 400, "POST"),
        (StepNames.AuthPut400, 400, "PUT"),
        (StepNames.AuthDelete202, 202, "DELETE"),
        (StepNames.FhirAuthGet404, 404, "GET"),
        (StepNames.FhirAuthPut400, 400, "PUT"),
        (StepNames.FacilityValidateGet400, 400, "GET"),
        (StepNames.FhirServerValidateGet400, 400, "GET"),
        (StepNames.LogsByFacilityGet200, 200, "GET"),
        (StepNames.LogsByPatientGet200, 200, "GET"),
        (StepNames.StatisticsGet200, 200, "GET"),
        (StepNames.ResourceCountsGet200, 200, "GET"),
        (StepNames.ResourceCountsGet400, 400, "GET"),
        (StepNames.UpdateLog400, 400, "PUT"),
        (StepNames.OrgLocationConfigGet200ById, 200, "GET"),
        (StepNames.OrgLocationConfigGet404, 404, "GET"),
        (StepNames.OrgLocationConfigSearch200, 200, "GET"),
        (StepNames.OrgLocationConfigPut404, 404, "PUT"),
        (StepNames.OrgLocationConfigPut202ById, 202, "PUT"),
        (StepNames.OrgLocationConfigPut202, 202, "PUT"),
        (StepNames.OrgLocationConfigDelete202ById, 202, "DELETE"),
        (StepNames.OrgLocationConfigDelete202, 202, "DELETE"),
        (StepNames.OrgLocationMappingGet404, 404, "GET"),
        (StepNames.OrgLocationMappingSearch200, 200, "GET"),
        (StepNames.OrgLocationMappingPut404, 404, "PUT"),
        (StepNames.OrgLocationMappingDelete202, 202, "DELETE"),
        (StepNames.OrgLocationMappingsDelete202, 202, "DELETE"),
        (StepNames.EncounterMappingGet404, 404, "GET"),
        (StepNames.EncounterMappingByEncounterGet404, 404, "GET"),
        (StepNames.EncounterMappingsByPatientGet200, 200, "GET"),
        (StepNames.EncounterMappingSearch200, 200, "GET"),
        (StepNames.EncounterMappingPut404, 404, "PUT"),
        (StepNames.EncounterMappingPutByEncounter404, 404, "PUT"),
        (StepNames.EncounterMappingPost400, 400, "POST"),
        (StepNames.FhirQueriesGet404, 404, "GET"),
        (StepNames.SftpConfigGet400, 400, "GET"),
        (StepNames.SftpConfigByOrgGet404, 404, "GET"),
        (StepNames.SftpConfigPost400, 400, "POST"),
        (StepNames.SftpConfigPut400, 400, "PUT"),
        (StepNames.SftpConfigDelete400, 400, "DELETE"),
        (StepNames.SftpCredentialsPut400, 400, "PUT"),
        (StepNames.SftpCredentialStatusGet404, 404, "GET"),
        (StepNames.SftpCredentialsDelete404, 404, "DELETE"),
        (StepNames.SftpSavedTestPost404, 404, "POST"),
        (StepNames.SftpAdHocTestPost400, 400, "POST"),
        (StepNames.SftpLogsGet200, 200, "GET"),
        (StepNames.SftpLogGet400, 400, "GET"),
        (StepNames.SftpLogPost400, 400, "POST"),
        (StepNames.SftpLogReset400, 400, "POST")
    ];

    public class RecordingDataAcquisitionClient : DispatchProxy
    {
        private readonly List<RecordedCall> _calls = [];

        public int ReturnedConfigId { get; set; } = CreatedConfigId;

        public IReadOnlyList<RecordedCall> Calls(string name) =>
            _calls.Where(call => call.Name == name).ToList();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var method = targetMethod ?? throw new InvalidOperationException("Missing method.");
            var callArgs = args ?? [];
            _calls.Add(new RecordedCall(method.Name, callArgs));

            var responseType = method.ReturnType.GetGenericArguments()[0];
            var status = StatusFor(method.Name, callArgs);
            object? body = method.Name == nameof(IDataAcquisitionServiceClient.CreateOrganizationLocationConfigurationAsync)
                ? new OrganizationLocationConfigurationApiModel { ConfigId = ReturnedConfigId }
                : null;
            var response = CreateResponse(responseType, status, body);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(responseType)
                .Invoke(null, [response]);
        }

        private static int StatusFor(string name, object?[] args) => name switch
        {
            nameof(IDataAcquisitionServiceClient.CreateOrganizationLocationConfigurationAsync) => 201,
            nameof(IDataAcquisitionServiceClient.GetOrganizationLocationConfigurationByIdAsync) =>
                args[0] is int id && id == CreatedConfigId ? 200 : 404,
            nameof(IDataAcquisitionServiceClient.UpdateOrganizationLocationConfigurationByIdAsync) =>
                args[0] is int updateId && updateId == CreatedConfigId ? 202 : 404,
            nameof(IDataAcquisitionServiceClient.UpdateOrganizationLocationConfigurationAsync) => 202,
            nameof(IDataAcquisitionServiceClient.DeleteOrganizationLocationConfigurationByIdAsync) => 202,
            nameof(IDataAcquisitionServiceClient.DeleteOrganizationLocationConfigurationsAsync) => 202,
            nameof(IDataAcquisitionServiceClient.GetOrganizationLocationMappingAsync) => 404,
            nameof(IDataAcquisitionServiceClient.UpdateOrganizationLocationMappingAsync) => 404,
            nameof(IDataAcquisitionServiceClient.DeleteOrganizationLocationMappingAsync) => 202,
            nameof(IDataAcquisitionServiceClient.DeleteOrganizationLocationMappingsByFacilityAsync) => 202,
            nameof(IDataAcquisitionServiceClient.GetEncounterMappingByIdAsync) => 404,
            nameof(IDataAcquisitionServiceClient.GetEncounterMappingByEncounterAsync) => 404,
            nameof(IDataAcquisitionServiceClient.UpdateEncounterMappingAsync) => 404,
            nameof(IDataAcquisitionServiceClient.UpdateEncounterMappingByEncounterAsync) => 404,
            nameof(IDataAcquisitionServiceClient.CreateEncounterMappingAsync) => 400,
            nameof(IDataAcquisitionServiceClient.UpdateFhirQueryConfigurationAsync) => 202,
            nameof(IDataAcquisitionServiceClient.GetAuthenticationConfigurationAsync) => 404,
            nameof(IDataAcquisitionServiceClient.CreateAuthenticationConfigurationAsync) => 400,
            nameof(IDataAcquisitionServiceClient.UpdateAuthenticationConfigurationAsync) => 400,
            nameof(IDataAcquisitionServiceClient.DeleteAuthenticationConfigurationAsync) => 202,
            nameof(IDataAcquisitionServiceClient.UpdateQueryPlanAsync) => 202,
            nameof(IDataAcquisitionServiceClient.DeleteAllQueryPlansAsync) => 202,
            nameof(IDataAcquisitionServiceClient.GetReportStatisticsAsync) => 200,
            nameof(IDataAcquisitionServiceClient.GetAcquiredResourceCountsByPatientAsync) =>
                args[1] is string reportId && Guid.TryParse(reportId, out _) ? 200 : 400,
            nameof(IDataAcquisitionServiceClient.UpdateAcquisitionLogAsync) => 400,
            nameof(IDataAcquisitionServiceClient.GetFhirAuthenticationConfigurationAsync) => 404,
            nameof(IDataAcquisitionServiceClient.UpdateFhirAuthenticationConfigurationAsync) => 400,
            nameof(IDataAcquisitionServiceClient.ValidateFacilityConnectionAsync) => 400,
            nameof(IDataAcquisitionServiceClient.ValidateFhirServerConnectionAsync) => 400,
            nameof(IDataAcquisitionServiceClient.GetFhirQueriesAsync) => 404,
            nameof(IDataAcquisitionServiceClient.GetSftpConfigurationByIdAsync) => 400,
            nameof(IDataAcquisitionServiceClient.GetOrganizationSftpConfigurationAsync) => 404,
            nameof(IDataAcquisitionServiceClient.CreateSftpConfigurationAsync) => 400,
            nameof(IDataAcquisitionServiceClient.UpdateSftpConfigurationAsync) => 400,
            nameof(IDataAcquisitionServiceClient.DeleteSftpConfigurationAsync) => 400,
            nameof(IDataAcquisitionServiceClient.UpdateSftpCredentialsAsync) => 400,
            nameof(IDataAcquisitionServiceClient.GetSftpCredentialStatusAsync) => 404,
            nameof(IDataAcquisitionServiceClient.DeleteSftpCredentialsAsync) => 404,
            nameof(IDataAcquisitionServiceClient.TestSavedSftpConnectionAsync) => 404,
            nameof(IDataAcquisitionServiceClient.TestSftpConnectionAsync) => 400,
            nameof(IDataAcquisitionServiceClient.GetSftpLogAsync) => 400,
            nameof(IDataAcquisitionServiceClient.CreateSftpLogAsync) => 400,
            nameof(IDataAcquisitionServiceClient.ResetSftpLogAsync) => 400,
            _ => 200
        };

        private static object CreateResponse(Type responseType, int statusCode, object? body)
        {
            var constructor = responseType.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException($"No constructor for {responseType.Name}.");
            var bindings = new List<MemberBinding>
            {
                LinqExpr.Bind(responseType.GetProperty(nameof(LinkApiResponse.StatusCode))!, LinqExpr.Constant(statusCode))
            };
            if (body is not null)
            {
                var bodyProperty = responseType.GetProperty(nameof(LinkApiResponse<OrganizationLocationConfigurationApiModel>.Body))!;
                bindings.Add(LinqExpr.Bind(bodyProperty, LinqExpr.Convert(LinqExpr.Constant(body), bodyProperty.PropertyType)));
            }

            return LinqExpr.Lambda<Func<object>>(
                LinqExpr.Convert(LinqExpr.MemberInit(LinqExpr.New(constructor), bindings), typeof(object))).Compile()();
        }
    }

    public sealed record RecordedCall(string Name, object?[] Args);
}
