using System.Reflection;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Link.UI.Tests;

public class FacilityAcquisitionServiceTests
{
    [Fact]
    public async Task Save_fhir_list_updates_and_keeps_stored_auth_when_the_form_says_it_is_new()
    {
        const string stored = """{"authentication":{"authType":"Basic","userName":"kept-user"}}""";
        var (client, state) = FhirListClient.Create(new LinkApiResponse { StatusCode = 200, RawBody = stored });
        var service = new FacilityAcquisitionService(client, NullLogger.Instance);

        var error = await service.SaveFhirListAsync(Page(), Panel(exists: false), CancellationToken.None);

        error.Should().BeNull();
        state.Updates.Should().Be(1);
        state.Creates.Should().Be(0);
        var saved = state.SavedBody.Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        var auth = saved["Authentication"].Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        auth["authType"].Should().Be("Basic");
        auth["userName"].Should().Be("kept-user");
    }

    [Fact]
    public async Task Save_fhir_list_creates_without_auth_when_the_server_has_no_config()
    {
        var (client, state) = FhirListClient.Create(new LinkApiResponse { StatusCode = 404 });
        var service = new FacilityAcquisitionService(client, NullLogger.Instance);

        var error = await service.SaveFhirListAsync(Page(), Panel(exists: true), CancellationToken.None);

        error.Should().BeNull();
        state.Creates.Should().Be(1);
        state.Updates.Should().Be(0);
        var saved = state.SavedBody.Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        saved.Should().NotContainKey("Authentication");
    }

    [Fact]
    public async Task Save_fhir_list_does_not_write_when_the_lookup_fails()
    {
        var (client, state) = FhirListClient.Create(new LinkApiResponse { StatusCode = 500, RawBody = "unavailable" });
        var service = new FacilityAcquisitionService(client, NullLogger.Instance);

        var error = await service.SaveFhirListAsync(Page(), Panel(exists: false), CancellationToken.None);

        error.Should().NotBeNullOrWhiteSpace();
        state.Creates.Should().Be(0);
        state.Updates.Should().Be(0);
    }

    private static FacilityHubViewModel Page() => new() { FacilityId = "facility-1" };

    private static FhirListPanel Panel(bool exists) => new()
    {
        Exists = exists,
        FhirBaseServerUrl = "https://fhir.example.org/api/FHIR/R4",
        Lists = FacilityAcquisitionRules.PatientListSlots.Select((slot, index) => new PatientListInput
        {
            Status = slot.Status,
            TimeFrame = slot.TimeFrame,
            FhirId = "list-" + index
        }).ToList()
    };

    private class FhirListClient : DispatchProxy
    {
        public LinkApiResponse GetResult { get; set; } = new() { StatusCode = 404 };
        public int Creates { get; private set; }
        public int Updates { get; private set; }
        public object? SavedBody { get; private set; }

        public static (IDataAcquisitionServiceClient Client, FhirListClient State) Create(LinkApiResponse current)
        {
            var client = DispatchProxy.Create<IDataAcquisitionServiceClient, FhirListClient>();
            var state = (FhirListClient)(object)client;
            state.GetResult = current;
            return (client, state);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IDataAcquisitionServiceClient.GetFhirListConfigurationAsync):
                    return Task.FromResult(GetResult);
                case nameof(IDataAcquisitionServiceClient.CreateFhirListConfigurationAsync):
                    Creates++;
                    SavedBody = args?[0];
                    return Task.FromResult(new LinkApiResponse { StatusCode = 201 });
                case nameof(IDataAcquisitionServiceClient.UpdateFhirListConfigurationAsync):
                    Updates++;
                    SavedBody = args?[0];
                    return Task.FromResult(new LinkApiResponse { StatusCode = 200 });
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
