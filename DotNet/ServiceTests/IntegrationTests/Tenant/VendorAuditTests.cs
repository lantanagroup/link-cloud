using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

/// <summary>
/// LEGLINK-1337: Tenant's vendor endpoints through the shared mechanism - the real controller,
/// manager, repository and database, with the filter around the action and the real
/// KafkaAuditPublisher producing into the fixture's recording producer. Only the broker and
/// Normalization are stubbed.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class VendorAuditTests : IDisposable
{
    private const string CallerEmail = "vendor.admin@example.org";

    private readonly IServiceScope _scope;
    private readonly IVendorManager _vendorManager;
    private readonly AuditedEndpoint<VendorController> _endpoint;
    private readonly TenantIntegrationTestFixture.RecordingAuditProducer _auditProducer;

    public VendorAuditTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var serviceProvider = _scope.ServiceProvider;

        _vendorManager = serviceProvider.GetRequiredService<IVendorManager>();
        _auditProducer = serviceProvider.GetRequiredService<TenantIntegrationTestFixture.RecordingAuditProducer>();
        _auditProducer.Clear();

        _endpoint = new AuditedEndpoint<VendorController>(
            new VendorController(
                serviceProvider.GetRequiredService<ILogger<VendorController>>(),
                _vendorManager,
                serviceProvider.GetRequiredService<IVendorQueries>()),
            serviceProvider,
            CallerEmail);

        // Deleting a vendor checks Normalization for references to each of its versions.
        var normalizationServiceClient = serviceProvider.GetRequiredService<Mock<INormalizationServiceClient>>();
        normalizationServiceClient.Reset();
        normalizationServiceClient
            .Setup(client => client.GetVendorVersionOperationPresetsAsync(
                It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<NormalizationVendorVersionOperationPresetApiModel>>
            {
                StatusCode = StatusCodes.Status200OK,
                Body = []
            });
    }

    public void Dispose() => _scope.Dispose();

    private async Task<VendorModel> AVendor(VendorAuthenticationSettings? authentication = null)
    {
        var created = await _vendorManager.CreateVendorAsync(new VendorModel
        {
            Name = $"Vendor-{Guid.NewGuid():N}",
            Authentication = authentication
        });

        _auditProducer.Clear();
        return created;
    }

    /// <summary>
    /// A vendor is not facility-scoped, so the events carry no facility and the Kafka key is empty.
    /// </summary>
    private List<(string Key, AuditEventMessage Value)> VendorEvents() =>
        _auditProducer.Produced.Where(produced => produced.Value.Resource == "Vendor").ToList();

    [Fact]
    public async Task PostVendor_PublishesACreateEventNamingTheNewVendor()
    {
        VendorModel? created = null;

        await _endpoint.RunAsync<VendorModel>(nameof(VendorController.Post), new(),
            async () =>
            {
                var result = await _endpoint.Controller.Post(new CreateVendorModel
                {
                    Name = $"Vendor-{Guid.NewGuid():N}"
                });

                created = (result.Result as CreatedAtActionResult)?.Value as VendorModel;
                return result;
            });

        var published = Assert.Single(VendorEvents());

        //the id only exists once the vendor does, so the handler puts it on the context.
        Assert.Equal(created!.Id.ToString(), published.Value.ResourceId);
        Assert.Equal(AuditEventType.Create, published.Value.Action);
        Assert.Equal(TenantConstants.ServiceName, published.Value.ServiceName);
        Assert.Equal(CallerEmail, published.Value.User);
        Assert.Null(published.Value.CorrelationId);
        Assert.Null(published.Value.Notes);

        //nothing facility-scoped to key on, which is what the old sender used the vendor id for.
        Assert.Null(published.Value.FacilityId);
        Assert.Equal(string.Empty, published.Key);
    }

    [Fact]
    public async Task PutVendor_PublishesAnUpdateEventDescribingTheRename()
    {
        var created = await AVendor();

        await _endpoint.RunAsync<VendorModel>(nameof(VendorController.Put), "id", created.Id!.Value.ToString(),
            () => _endpoint.Controller.Put(created.Id!.Value.ToString(), new UpdateVendorModel
            {
                Name = "Renamed Vendor"
            }));

        var published = Assert.Single(VendorEvents()).Value;
        Assert.Equal(AuditEventType.Update, published.Action);
        Assert.Equal(created.Id.ToString(), published.ResourceId);
        Assert.Equal(CallerEmail, published.User);

        var name = Assert.Single(published.PropertyChanges!,
            change => change.PropertyName == nameof(VendorModel.Name));
        Assert.Equal(created.Name, name.InitialPropertyValue);
        Assert.Equal("Renamed Vendor", name.NewPropertyValue);
    }

    [Fact]
    public async Task PutVendor_RecordsASigningKeyChangeWithoutRecordingTheKey()
    {
        var created = await AVendor();

        await _endpoint.RunAsync<VendorModel>(nameof(VendorController.Put), "id", created.Id!.Value.ToString(),
            () => _endpoint.Controller.Put(created.Id!.Value.ToString(), new UpdateVendorModel
            {
                Name = created.Name,
                Authentication = new VendorAuthenticationSettings { SigningKeySecretId = "epic-signing-key" }
            }));

        var published = Assert.Single(VendorEvents()).Value;

        //Vendor.Authentication is a value-converted JSON column, so the whole settings object is one
        //property - and "Authentication" is on the shared masked-name list. The event says that
        //authentication changed, not what it changed to.
        var authentication = Assert.Single(published.PropertyChanges!,
            change => change.PropertyName == nameof(VendorModel.Authentication));
        Assert.Equal(AuditOptions.MaskedValue, authentication.InitialPropertyValue);
        Assert.Equal(AuditOptions.MaskedValue, authentication.NewPropertyValue);
        Assert.DoesNotContain("epic-signing-key", System.Text.Json.JsonSerializer.Serialize(published));
    }

    [Fact]
    public async Task PutVendor_ThatChangesNothing_StillPublishesAnEventWithNoChanges()
    {
        var created = await AVendor();

        await _endpoint.RunAsync<VendorModel>(nameof(VendorController.Put), "id", created.Id!.Value.ToString(),
            () => _endpoint.Controller.Put(created.Id!.Value.ToString(), new UpdateVendorModel
            {
                Name = created.Name
            }));

        //the shared rule, and a change from Tenant's old sender: the filter publishes on any
        //successful response, so a no-op write is still recorded as having been made.
        var published = Assert.Single(VendorEvents()).Value;
        Assert.Equal(AuditEventType.Update, published.Action);
        Assert.Empty(published.PropertyChanges ?? []);
    }

    [Fact]
    public async Task DeleteVendor_PublishesADeleteEventNamingTheVendor()
    {
        var created = await AVendor();

        await _endpoint.RunAsync(nameof(VendorController.Delete), "id", created.Id!.Value.ToString(),
            () => _endpoint.Controller.Delete(created.Id!.Value.ToString()));

        var published = Assert.Single(VendorEvents()).Value;
        Assert.Equal(AuditEventType.Delete, published.Action);
        Assert.Equal(created.Id.ToString(), published.ResourceId);
        Assert.Equal(CallerEmail, published.User);
        Assert.Null(published.Notes);
    }

    [Fact]
    public async Task ARejectedPut_PublishesNothing()
    {
        await _endpoint.RunAsync<VendorModel>(nameof(VendorController.Put), "id", "not-a-guid",
            () => _endpoint.Controller.Put("not-a-guid", new UpdateVendorModel { Name = "Nobody" }));

        Assert.Empty(VendorEvents());
    }

    [Fact]
    public void TheVendorEndpoints_AreMarkedAsTheAdrsTenantTableSays()
    {
        var post = AuditedEndpoint<VendorController>.AttributeOn(nameof(VendorController.Post));
        Assert.Equal(AuditEventType.Create, post.Action);
        Assert.Equal("Vendor", post.Resource);

        var put = AuditedEndpoint<VendorController>.AttributeOn(nameof(VendorController.Put));
        Assert.Equal(AuditEventType.Update, put.Action);
        Assert.Equal("Vendor", put.Resource);

        var delete = AuditedEndpoint<VendorController>.AttributeOn(nameof(VendorController.Delete));
        Assert.Equal(AuditEventType.Delete, delete.Action);
        Assert.Equal("Vendor", delete.Resource);

        //the ADR's Tenant rows say "Remove Notes. Remove CorrelationId."; the route's own {id} is
        //the record id, so none of the three needs a RecordIdRouteKey of its own.
        Assert.All([post, put, delete], auditable =>
        {
            Assert.Null(auditable.Notes);
            Assert.Equal("id", auditable.RecordIdRouteKey);
        });
    }
}
