using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using ServiceTests.IntegrationTests.Normalization;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{
    [Collection("NormalizationIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class NormalizationManagerTests
    {
        private readonly ITestOutputHelper _output;
        private readonly NormalizationIntegrationTestFixture _fixture;
        private readonly IDatabase _database;
        private readonly IOperationManager _operationManager;
        private readonly IVendorOperationPresetManager _vendorPresetManager;
        private readonly IOperationSequenceQueries _operationSequenceQueries;

        public NormalizationManagerTests(NormalizationIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            _operationManager = _fixture.ServiceProvider.GetRequiredService<IOperationManager>();
            _operationSequenceQueries = _fixture.ServiceProvider.GetRequiredService<IOperationSequenceQueries>();
            _vendorPresetManager = _fixture.ServiceProvider.GetRequiredService<IVendorOperationPresetManager>();
        }


        [Fact]
        public async Task OperationSequence_Can_Create_Get_Delete()
        {

            var vendor = await _vendorPresetManager.CreateVendorOperationPreset(new CreateVendorOperationPresetModel()
            {
                Vendor = "Test Vendor",
                Description = "Test Vendor",
                Versions = "1.0, 1.1"
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
                VendorPresetIds = [vendor.Id]
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
            Assert.Equal(vendor.Id, sequences[0].VendorPresets[0].Id);
            Assert.Equal("Test Vendor", sequences[0].VendorPresets[0].Vendor);
            Assert.Equal("Test Vendor", sequences[0].VendorPresets[0].Description);
            Assert.Equal("1.0, 1.1", sequences[0].VendorPresets[0].Versions);

            var deleteResult = await _operationManager.DeleteOperationSequence(new DeleteOperationSequencesModel()
            {
                FacilityId = facilityId
            });

            Assert.True(deleteResult);
        }

    }
}
