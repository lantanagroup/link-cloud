using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{

    [Collection("NormalizationIntegrationTests")]
    public class VendorTests
    {
        private readonly NormalizationIntegrationTestFixture _fixture;

        public VendorTests(NormalizationIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task EnsureResourcesCreated()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IResourceManager>();

            var result = await manager.InitializeResources();
        }

        [Fact]
        public async Task CreateVendor_NewName_CreatesSuccessfully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();
            var vendorQueries = scope.ServiceProvider.GetRequiredService<IVendorQueries>();

            string vendorName = Guid.NewGuid().ToString();
            var result = await vendorManager.CreateVendor(vendorName);

            Assert.NotNull(result);
            Assert.Equal(vendorName, result.Name);

            var fetched = await vendorQueries.GetVendor(vendorName);
            Assert.NotNull(fetched);
        }

        [Fact]
        public async Task CreateVendor_ExistingName_ReturnsNull()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();

            string vendorName = Guid.NewGuid().ToString();
            await vendorManager.CreateVendor(vendorName);

            var result = await vendorManager.CreateVendor(vendorName);

            Assert.Null(result);
        }

        [Fact]
        public async Task CreateVendorVersion_Valid_CreatesSuccessfully()
        {
            var vendorManager = _fixture.ServiceProvider.GetRequiredService<IVendorManager>();
            var vendorQueries = _fixture.ServiceProvider.GetRequiredService<IVendorQueries>();

            string vendorName = Guid.NewGuid().ToString();
            var vendor = await vendorManager.CreateVendor(vendorName);
            var model = vendor?.Versions.Single();

            Assert.NotNull(model);
            Assert.Equal("default", model.Version);

            var fetched = await vendorQueries.GetVendor(vendorName);
            Assert.Single(fetched.Versions);
        }

        [Fact]
        public async Task CreateVendorVersionOperationPreset_Valid_CreatesSuccessfully()
        {
            await EnsureResourcesCreated();

            var vendorManager = _fixture.ServiceProvider.GetRequiredService<IVendorManager>();
            var vendorQueries = _fixture.ServiceProvider.GetRequiredService<IVendorQueries>();
            var operationManager = _fixture.ServiceProvider.GetRequiredService<IOperationManager>();

            // Create vendor and version
            string vendorName = Guid.NewGuid().ToString();
            var vendor = await vendorManager.CreateVendor(vendorName);

            // Create operation
            var vendorIds = new List<Guid> { vendor.Id };
            var opModel = new CreateOperationModel
            {
                VendorIds = vendorIds,
                OperationType = "CopyProperty",
                OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
                ResourceTypes = new List<string> { "Patient" },
                Name = "TestOp",
                Description = "Test",
                IsDisabled = false
            };

            var opResult = await operationManager.CreateOperation(opModel);
            Assert.True(opResult.IsSuccess, opResult.ErrorMessage);
            var op = (OperationModel)opResult.ObjectResult;
            var ortId = op.OperationResourceTypes.First().Id;

            // Create preset to the same operation for a second vendor
            var vendor2 = await vendorManager.CreateVendor(Guid.NewGuid().ToString());
            var versionModel2 = vendor2?.Versions.Single();
            Assert.NotNull(versionModel2);

            var presetModel = new CreateVendorVersionOperationPresetModel { VendorVersionId = versionModel2.Id, OperationResourceTypeId = ortId };
            var result = await vendorManager.CreateVendorVersionOperationPreset(presetModel);

            Assert.NotNull(result);

            var fetched = await vendorQueries.SearchVendorVersionOperationPreset(new VendorOperationPresetSearchModel()
            {
                VendorId = vendor.Id,
            });

            Assert.Single(fetched);

            fetched = await vendorQueries.SearchVendorVersionOperationPreset(new VendorOperationPresetSearchModel()
            {
                VendorId = vendor2.Id,
            });

            Assert.Single(fetched);
        }

        [Fact]
        public async Task DeleteVendor_ByName_DeletesSuccessfully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();
            var vendorQueries = scope.ServiceProvider.GetRequiredService<IVendorQueries>();

            string vendorName = Guid.NewGuid().ToString();
            await vendorManager.CreateVendor(vendorName);

            await vendorManager.DeleteVendor(vendorName);

            var fetched = await vendorQueries.GetVendor(vendorName);
            Assert.Null(fetched);
        }

        [Fact]
        public async Task DeleteVendor_ById_DeletesSuccessfully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();
            var vendorQueries = scope.ServiceProvider.GetRequiredService<IVendorQueries>();

            string vendorName = Guid.NewGuid().ToString();
            var vendor = await vendorManager.CreateVendor(vendorName);

            await vendorManager.DeleteVendor(vendor.Id);

            var fetched = await vendorQueries.GetVendor(vendorName);
            Assert.Null(fetched);
        }

        [Fact]
        public async Task DeleteVendor_NonExistingName_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();

            string nonExisting = Guid.NewGuid().ToString();
            await Assert.ThrowsAsync<InvalidOperationException>(() => vendorManager.DeleteVendor(nonExisting));
        }

        [Fact]
        public async Task DeleteVendorVersionOperationPreset_Valid_DeletesSuccessfully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();
            var vendorQueries = scope.ServiceProvider.GetRequiredService<IVendorQueries>();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            // Setup vendor, version, op, preset
            string vendorName = Guid.NewGuid().ToString();
            var vendor = await vendorManager.CreateVendor(vendorName);
            var versionModel = new CreateVendorVersionModel { VendorId = vendor.Id, Version = "default" };
            var version = await vendorManager.CreateVendorVersion(versionModel);

            var vendorIds = new List<Guid> { vendor.Id };
            var opModel = new CreateOperationModel
            {
                VendorIds = vendorIds,
                OperationType = "CopyProperty",
                OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
                ResourceTypes = new List<string> { "Patient" },
                Name = "TestOp",
                Description = "Test",
                IsDisabled = false
            };
            var opResult = await operationManager.CreateOperation(opModel);

            Assert.True(opResult.IsSuccess, opResult.ErrorMessage);

            var op = (OperationModel)opResult.ObjectResult;
            var ortId = op.OperationResourceTypes.First().Id;

            var presetModel = new CreateVendorVersionOperationPresetModel { VendorVersionId = version.Id, OperationResourceTypeId = ortId };
            var preset = await vendorManager.CreateVendorVersionOperationPreset(presetModel);

            await vendorManager.DeleteVendorVersionOperationPreset(vendor.Id, preset.Id);

            var presets = await vendorQueries.SearchVendorVersionOperationPreset(new VendorOperationPresetSearchModel()
            {
                Id = preset.Id
            });

            Assert.Empty(presets);
        }

        [Fact]
        public async Task DeleteVendorVersionOperationPreset_NonExisting_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => vendorManager.DeleteVendorVersionOperationPreset(Guid.NewGuid(), Guid.NewGuid()));
        }
    }

}