using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class OperationModelTests
{
    [Fact]
    public void PostOperationModel_CopyLocation_AddsLocationWhenNotPresent()
    {
        var model = new PostOperationModel(
            resourceTypes: [],
            operation: new CopyLocationOperation(),
            facilityId: null,
            vendorIds: null);

        Assert.Single(model.ResourceTypes);
        Assert.Contains("Location", model.ResourceTypes);
    }

    [Fact]
    public void PostOperationModel_CopyLocation_DoesNotDuplicateLocationWhenAlreadyPresent()
    {
        var model = new PostOperationModel(
            resourceTypes: ["Location"],
            operation: new CopyLocationOperation(),
            facilityId: null,
            vendorIds: null);

        Assert.Single(model.ResourceTypes);
        Assert.Equal("Location", model.ResourceTypes[0]);
    }

    [Fact]
    public void PutOperationModel_CopyLocation_AddsLocationWhenNotPresent()
    {
        var model = new PutOperationModel(
            id: Guid.NewGuid(),
            resourceTypes: [],
            operation: new CopyLocationOperation(),
            isDisabled: false,
            facilityId: null,
            vendorIds: null);

        Assert.Single(model.ResourceTypes);
        Assert.Contains("Location", model.ResourceTypes);
    }

    [Fact]
    public void PutOperationModel_CopyLocation_DoesNotDuplicateLocationWhenAlreadyPresent()
    {
        var model = new PutOperationModel(
            id: Guid.NewGuid(),
            resourceTypes: ["Location"],
            operation: new CopyLocationOperation(),
            isDisabled: false,
            facilityId: null,
            vendorIds: null);

        Assert.Single(model.ResourceTypes);
        Assert.Equal("Location", model.ResourceTypes[0]);
    }

    [Fact]
    public void CopyLocationAliasToTypeIterativelyOperation_UsesExpectedDefaults()
    {
        var operation = new CopyLocationAliasToTypeIterativelyOperation();

        Assert.Equal(OperationType.CopyLocationAliasToTypeIteratively, operation.OperationType);
        Assert.Equal(15, operation.MaxIterations);
        Assert.False(operation.SplitOnComma);
    }

    [Fact]
    public void PostOperationModel_CopyLocationAliasToTypeIteratively_AddsLocationWhenNotPresent()
    {
        var model = new PostOperationModel(
            resourceTypes: [],
            operation: new CopyLocationAliasToTypeIterativelyOperation(),
            facilityId: null,
            vendorIds: null);

        Assert.Single(model.ResourceTypes);
        Assert.Contains("Location", model.ResourceTypes);
    }

    [Fact]
    public void PutOperationModel_CopyLocationAliasToTypeIteratively_AddsLocationWhenNotPresent()
    {
        var model = new PutOperationModel(
            id: Guid.NewGuid(),
            resourceTypes: [],
            operation: new CopyLocationAliasToTypeIterativelyOperation(),
            isDisabled: false,
            facilityId: null,
            vendorIds: null);

        Assert.Single(model.ResourceTypes);
        Assert.Contains("Location", model.ResourceTypes);
    }
}
