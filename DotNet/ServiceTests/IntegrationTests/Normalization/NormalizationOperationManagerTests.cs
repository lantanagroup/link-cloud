using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain.Managers;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.IntegrationTests.Normalization
{
    [Collection("NormalizationIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class NormalizationOperationManagerTests
    {
        private readonly NormalizationIntegrationTestFixture _fixture;
        private readonly IOperationManager _operationManager;
        private readonly IVendorManager _vendorManager;

        public NormalizationOperationManagerTests(NormalizationIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _operationManager = _fixture.ServiceProvider.GetRequiredService<IOperationManager>();
            _vendorManager = _fixture.ServiceProvider.GetRequiredService<IVendorManager>();
        }


        [Fact]
        public async Task OperationSequence_Can_Create_Get_Delete()
        {
            var vendor = await _vendorManager.CreateVendor("Test Vendor");

            var vendorVersion = await _vendorManager.CreateVendorVersion(new CreateVendorVersionModel()
            {
                VendorId = vendor.Id,
                Version = "1.0"
            });

            var facilityId = Guid.NewGuid().ToString();
            var operation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Location"],
                VendorIds = [vendor.Id]
            });

            var postModel = new List<PostOperationSequence>()
            {
                new PostOperationSequence()
                {
                    OperationId = result.Id,
                    Sequence = 1,
                }
            };

            var sequences = await _operationManager.CreateOperationSequences(new CreateOperationSequencesModel()
            {
                FacilityId = facilityId,
                ResourceType = "Location",
                OperationSequences = postModel.Select(a => new CreateOperationSequenceModel
                {
                    OperationId = a.OperationId,
                    Sequence = a.Sequence,
                }).ToList()
            });

            Assert.NotEmpty(sequences);
            Assert.Equal(facilityId, sequences[0].FacilityId);
            Assert.Equal(1, sequences[0].Sequence);
            Assert.Contains("Copy Location Identifier to Type", sequences[0].OperationResourceType.Operation.OperationJson);
            Assert.NotEmpty(sequences[0].VendorPresets);
            Assert.Equal(result.VendorPresets[0].Id, sequences[0].VendorPresets[0].Id);
            Assert.Equal("Test Vendor", sequences[0].VendorPresets[0].VendorVersion.Vendor.Name);
            Assert.Equal("1.0", sequences[0].VendorPresets[0].VendorVersion.Version);
            Assert.Equal("Location", sequences[0].VendorPresets[0].OperationResourceType.Resource.ResourceName);

            var deleteResult = await _operationManager.DeleteOperationSequence(new DeleteOperationSequencesModel()
            {
                FacilityId = facilityId
            });

            Assert.True(deleteResult);
        }
    }
}
