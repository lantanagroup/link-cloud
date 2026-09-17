using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class VendorVersionOperationPresetTests
{
    private readonly NormalizationIntegrationTestFixture _fixture;

    public VendorVersionOperationPresetTests(NormalizationIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAndDeletePreset_UsesVendorVersionId()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
        var presetManager = scope.ServiceProvider.GetRequiredService<IVendorVersionOperationPresetManager>();
        var presetQueries = scope.ServiceProvider.GetRequiredService<IVendorVersionOperationPresetQueries>();
        var primaryVendorVersionId = Guid.NewGuid();
        var additionalVendorVersionId = Guid.NewGuid();

        var createResult = await operationManager.CreateOperation(new CreateOperationModel
        {
            VendorVersionIds = [primaryVendorVersionId],
            OperationType = OperationType.CopyProperty.ToString(),
            OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
            ResourceTypes = ["Patient"],
            Name = "TestOp",
            Description = "Test",
            IsDisabled = false
        });

        Assert.True(createResult.IsSuccess, createResult.ErrorMessage);
        var operation = (OperationModel)createResult.ObjectResult!;
        var operationResourceTypeId = operation.OperationResourceTypes.Single().Id;

        var preset = await presetManager.Create(new CreateVendorVersionOperationPresetModel
        {
            VendorVersionId = additionalVendorVersionId,
            OperationResourceTypeId = operationResourceTypeId
        });

        Assert.Equal(additionalVendorVersionId, preset.VendorVersionId);
        Assert.Equal(additionalVendorVersionId, preset.VendorVersion.Id);

        var presets = await presetQueries.Search(new VendorVersionOperationPresetSearchModel
        {
            VendorVersionId = additionalVendorVersionId
        });
        Assert.Single(presets);

        await presetManager.Delete(additionalVendorVersionId, preset.Id);

        Assert.Null(await presetQueries.Get(preset.Id));
    }
}