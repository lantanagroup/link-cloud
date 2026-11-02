using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
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
/// LEGLINK-1337: the three VendorVersion endpoints, which emitted no audit events before this story.
/// The delete carries the only note in Tenant's table, and the values it names are read from the row
/// before the delete takes them away.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class VendorVersionAuditTests : IDisposable
{
    private const string CallerEmail = "version.admin@example.org";

    private readonly IServiceScope _scope;
    private readonly IVendorManager _vendorManager;
    private readonly AuditedEndpoint<VendorVersionController> _endpoint;
    private readonly TenantIntegrationTestFixture.RecordingAuditProducer _auditProducer;

    public VendorVersionAuditTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var serviceProvider = _scope.ServiceProvider;

        _vendorManager = serviceProvider.GetRequiredService<IVendorManager>();
        _auditProducer = serviceProvider.GetRequiredService<TenantIntegrationTestFixture.RecordingAuditProducer>();
        _auditProducer.Clear();

        _endpoint = new AuditedEndpoint<VendorVersionController>(
            new VendorVersionController(
                serviceProvider.GetRequiredService<ILogger<VendorVersionController>>(),
                _vendorManager,
                serviceProvider.GetRequiredService<IVendorQueries>()),
            serviceProvider,
            CallerEmail);

        // Deleting a version checks Normalization for presets that reference it.
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

    private async Task<VendorModel> AVendor() =>
        await _vendorManager.CreateVendorAsync(new VendorModel { Name = $"Vendor-{Guid.NewGuid():N}" });

    private async Task<(VendorModel Vendor, VendorVersionModel Version)> AVersion()
    {
        var vendor = await AVendor();
        var version = await _vendorManager.CreateVendorVersionAsync(new VendorVersionModel
        {
            VendorId = vendor.Id,
            Version = $"v{Guid.NewGuid():N}"
        });

        _auditProducer.Clear();
        return (vendor, version);
    }

    private List<(string Key, AuditEventMessage Value)> VersionEvents() =>
        _auditProducer.Produced.Where(produced => produced.Value.Resource == "VendorVersion").ToList();

    [Fact]
    public async Task PostVendorVersion_PublishesACreateEventNamingTheNewVersion()
    {
        var vendor = await AVendor();
        _auditProducer.Clear();

        VendorVersionModel? created = null;

        await _endpoint.RunAsync<VendorVersionModel>(nameof(VendorVersionController.Post), new(),
            async () =>
            {
                var result = await _endpoint.Controller.Post(new CreateVendorVersionModel
                {
                    VendorId = vendor.Id,
                    Version = "2026.11"
                });

                created = (result.Result as CreatedAtActionResult)?.Value as VendorVersionModel;
                return result;
            });

        var published = Assert.Single(VersionEvents());

        //the route carries no id on a create, so the handler sets it from the row it just made.
        Assert.Equal(created!.Id.ToString(), published.Value.ResourceId);
        Assert.Equal(AuditEventType.Create, published.Value.Action);
        Assert.Equal(TenantConstants.ServiceName, published.Value.ServiceName);
        Assert.Equal(CallerEmail, published.Value.User);
        Assert.Null(published.Value.CorrelationId);
        Assert.Null(published.Value.Notes);

        //a version belongs to a vendor, not a facility.
        Assert.Null(published.Value.FacilityId);
        Assert.Equal(string.Empty, published.Key);
    }

    [Fact]
    public async Task PutVendorVersion_PublishesAnUpdateEventDescribingTheVersionChange()
    {
        var (_, version) = await AVersion();

        await _endpoint.RunAsync<VendorVersionModel>(nameof(VendorVersionController.Put),
            "id", version.Id!.Value.ToString(),
            () => _endpoint.Controller.Put(version.Id!.Value.ToString(), new UpdateVendorVersionModel
            {
                Version = "2027.01"
            }));

        var published = Assert.Single(VersionEvents()).Value;
        Assert.Equal(AuditEventType.Update, published.Action);
        Assert.Equal(version.Id.ToString(), published.ResourceId);
        Assert.Equal(CallerEmail, published.User);
        Assert.Null(published.Notes);

        var changed = Assert.Single(published.PropertyChanges!,
            change => change.PropertyName == nameof(VendorVersionModel.Version));
        Assert.Equal(version.Version, changed.InitialPropertyValue);
        Assert.Equal("2027.01", changed.NewPropertyValue);
    }

    [Fact]
    public async Task DeleteVendorVersion_PublishesANoteNamingTheVendorAndVersion()
    {
        var (vendor, version) = await AVersion();

        await _endpoint.RunAsync(nameof(VendorVersionController.Delete),
            "id", version.Id!.Value.ToString(),
            () => _endpoint.Controller.Delete(version.Id!.Value.ToString()));

        var published = Assert.Single(VersionEvents()).Value;
        Assert.Equal(AuditEventType.Delete, published.Action);
        Assert.Equal(version.Id.ToString(), published.ResourceId);

        //the ADR's note, filled from the row the existence check read: the delete leaves nothing to
        //look the vendor name and version up from afterwards.
        Assert.Equal($"Id {version.Id} deleted (Vendor: {vendor.Name}, Version: {version.Version})",
            published.Notes);
    }

    [Fact]
    public async Task ARejectedDelete_PublishesNothing()
    {
        await _endpoint.RunAsync(nameof(VendorVersionController.Delete),
            "id", Guid.NewGuid().ToString(),
            () => _endpoint.Controller.Delete(Guid.NewGuid().ToString()));

        Assert.Empty(VersionEvents());
    }

    [Fact]
    public void TheVendorVersionEndpoints_AreMarkedAsTheStorysNewRowsSay()
    {
        var post = AuditedEndpoint<VendorVersionController>.AttributeOn(nameof(VendorVersionController.Post));
        Assert.Equal(AuditEventType.Create, post.Action);
        Assert.Equal("VendorVersion", post.Resource);
        Assert.Null(post.Notes);

        var put = AuditedEndpoint<VendorVersionController>.AttributeOn(nameof(VendorVersionController.Put));
        Assert.Equal(AuditEventType.Update, put.Action);
        Assert.Equal("VendorVersion", put.Resource);
        Assert.Null(put.Notes);

        var delete = AuditedEndpoint<VendorVersionController>.AttributeOn(nameof(VendorVersionController.Delete));
        Assert.Equal(AuditEventType.Delete, delete.Action);
        Assert.Equal("VendorVersion", delete.Resource);
        Assert.Equal("Id {id} deleted (Vendor: {Name}, Version: {Version})", delete.Notes);
    }
}
