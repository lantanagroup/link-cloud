using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class VendorAuditTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly IServiceScope _scope;
    private readonly IVendorManager _vendorManager;
    private readonly TenantIntegrationTestFixture.RecordingAuditProducer _auditProducer;

    public VendorAuditTests(TenantIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var serviceProvider = _scope.ServiceProvider;

        _vendorManager = serviceProvider.GetRequiredService<IVendorManager>();
        _auditProducer = serviceProvider.GetRequiredService<TenantIntegrationTestFixture.RecordingAuditProducer>();
        _auditProducer.Clear();

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

    [Fact]
    public async Task CreateVendor_EmitsACreateAuditEvent()
    {
        var created = await _vendorManager.CreateVendorAsync(new VendorModel { Name = $"Vendor-{Guid.NewGuid():N}" });

        var events = await _auditProducer.WaitForAsync(1, Timeout);

        var audit = Assert.Single(events);
        Assert.Equal(AuditEventType.Create, audit.Value.Action);
        Assert.Equal("Vendor", audit.Value.Resource);
        Assert.Equal(created.Id.ToString(), audit.Key);
    }

    [Fact]
    public async Task UpdateVendor_RecordsTheSigningKeySecretIdChange()
    {
        var created = await _vendorManager.CreateVendorAsync(new VendorModel { Name = $"Vendor-{Guid.NewGuid():N}" });
        _auditProducer.Clear();

        await _vendorManager.UpdateVendorAsync(created.Id!.Value, new VendorModel
        {
            Name = created.Name,
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = "epic-signing-key" }
        });

        var events = await _auditProducer.WaitForAsync(1, Timeout);

        var audit = Assert.Single(events);
        Assert.Equal(AuditEventType.Update, audit.Value.Action);
        var change = Assert.Single(audit.Value.PropertyChanges!,
            c => c.PropertyName == nameof(VendorAuthenticationSettings.SigningKeySecretId));
        Assert.Null(change.InitialPropertyValue);
        Assert.Equal("epic-signing-key", change.NewPropertyValue);
    }

    [Fact]
    public async Task UpdateVendor_RecordsAClearedSigningKeySecretId()
    {
        var created = await _vendorManager.CreateVendorAsync(new VendorModel
        {
            Name = $"Vendor-{Guid.NewGuid():N}",
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = "epic-signing-key" }
        });
        _auditProducer.Clear();

        await _vendorManager.UpdateVendorAsync(created.Id!.Value, new VendorModel
        {
            Name = created.Name,
            Authentication = new VendorAuthenticationSettings { SigningKeySecretId = null }
        });

        var events = await _auditProducer.WaitForAsync(1, Timeout);

        var change = Assert.Single(Assert.Single(events).Value.PropertyChanges!,
            c => c.PropertyName == nameof(VendorAuthenticationSettings.SigningKeySecretId));
        Assert.Equal("epic-signing-key", change.InitialPropertyValue);
        Assert.Null(change.NewPropertyValue);
    }

    [Fact]
    public async Task UpdateVendor_ThatChangesNothing_EmitsNoAuditEvent()
    {
        var created = await _vendorManager.CreateVendorAsync(new VendorModel { Name = $"Vendor-{Guid.NewGuid():N}" });
        _auditProducer.Clear();

        await _vendorManager.UpdateVendorAsync(created.Id!.Value, new VendorModel { Name = created.Name });

        await Task.Delay(200);

        Assert.Empty(_auditProducer.Produced);
    }

    [Fact]
    public async Task DeleteVendor_EmitsADeleteAuditEvent()
    {
        var created = await _vendorManager.CreateVendorAsync(new VendorModel { Name = $"Vendor-{Guid.NewGuid():N}" });
        _auditProducer.Clear();

        await _vendorManager.DeleteVendorAsync(created.Id!.Value);

        var events = await _auditProducer.WaitForAsync(1, Timeout);

        Assert.Equal(AuditEventType.Delete, Assert.Single(events).Value.Action);
    }
}
