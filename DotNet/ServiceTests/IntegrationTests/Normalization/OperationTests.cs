using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{
    [Collection("NormalizationIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class OperationTests
    {
        private readonly NormalizationIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public OperationTests(NormalizationIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task OperationSequence_Can_Create_Get_Delete()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();
            var vendorQueries = scope.ServiceProvider.GetRequiredService<IVendorQueries>();
            var operationSequenceQueries = scope.ServiceProvider.GetRequiredService<IOperationSequenceQueries>();

            var vendor = await vendorManager.CreateVendor(Guid.NewGuid().ToString());

            var vendorVersion = (await vendorQueries.SearchVendors(new VendorSearchModel()
            {
                VendorId = vendor.Id
            })).First().Versions.Single();

            var facilityId = Guid.NewGuid().ToString();
            var operation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            var taskResult = await operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Location"],
                VendorIds = [vendor.Id]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            var postModel = new List<PostOperationSequence>()
            {
                new PostOperationSequence()
                {
                    OperationId = result.Id,
                    Sequence = 1,
                }
            };

            var sequences = await operationManager.CreateOperationSequences(new CreateOperationSequencesModel()
            {
                FacilityId = facilityId,
                ResourceType = "Location",
                OperationSequences = postModel.Select(a => new CreateOperationSequenceModel
                {
                    OperationId = a.OperationId!.Value,
                    Sequence = a.Sequence!.Value,
                }).ToList()
            });

            Assert.NotEmpty(sequences);
            Assert.Equal(facilityId, sequences[0].FacilityId);
            Assert.Equal(1, sequences[0].Sequence);
            Assert.Contains("Copy Location Identifier to Type", sequences[0].OperationResourceType.Operation.OperationJson);
            Assert.NotEmpty(sequences[0].VendorPresets);
            Assert.Equal(result.VendorPresets[0].Id, sequences[0].VendorPresets[0].Id);
            Assert.Equal(vendor.Name, sequences[0].VendorPresets[0].VendorVersion.Vendor.Name);
            Assert.Equal("default", sequences[0].VendorPresets[0].VendorVersion.Version);
            Assert.Equal("Location", sequences[0].VendorPresets[0].OperationResourceType.Resource.ResourceName);

            var deleteResult = await operationManager.DeleteOperationSequence(new DeleteOperationSequencesModel()
            {
                FacilityId = facilityId
            });

            Assert.True(deleteResult);
        }

        private CreateOperationModel GetValidCreateModelWithFacility(string facilityId, List<string> resourceTypes)
        {
            return new CreateOperationModel
            {
                FacilityId = facilityId,
                OperationType = "CopyProperty", // Assuming valid type
                OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}", // Valid FHIR paths
                ResourceTypes = resourceTypes,
                Name = "TestOp",
                Description = "Test",
                IsDisabled = false
            };
        }

        private CreateOperationModel GetValidCreateModelWithVendors(List<Guid> vendorIds, List<string> resourceTypes)
        {
            return new CreateOperationModel
            {
                VendorIds = vendorIds,
                OperationType = "CopyProperty",
                OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
                ResourceTypes = resourceTypes,
                Name = "TestOp",
                Description = "Test",
                IsDisabled = false
            };
        }

        [Fact]
        public async Task CreateOperation_ValidWithFacility_Succeeds()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            string facilityId = Guid.NewGuid().ToString();
            var model = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });

            var result = await operationManager.CreateOperation(model);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.ObjectResult);

            var op = (OperationModel)result.ObjectResult;
            var created = await operationQueries.Get(op.Id, facilityId);
            Assert.NotNull(created);
        }

        [Fact]
        public async Task CreateOperation_ValidWithVendors_Succeeds()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            var vendor = await vendorManager.CreateVendor(Guid.NewGuid().ToString());
            Assert.NotNull(vendor);
            var vendorIds = new List<Guid> { vendor.Id };
            var model = GetValidCreateModelWithVendors(vendorIds, new List<string> { "Patient" });

            var result = await operationManager.CreateOperation(model);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.ObjectResult);

            var created = (OperationModel)result.ObjectResult;
            var searched = await operationQueries.Search(new OperationSearchModel { VendorId = vendorIds.First() });
            Assert.Contains(searched.Records, o => o.Id == created.Id);
        }

        [Fact]
        public async Task CreateOperation_WithBothFacilityAndVendors_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var model = GetValidCreateModelWithFacility(Guid.NewGuid().ToString(), new List<string> { "Patient" });
            model.VendorIds = new List<Guid> { Guid.NewGuid() };

            var result = await operationManager.CreateOperation(model);
            Assert.False(result.IsSuccess);
            Assert.Contains("but not both", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateOperation_WithNeitherFacilityNorVendors_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var model = GetValidCreateModelWithFacility(null, new List<string> { "Patient" });

            var result = await operationManager.CreateOperation(model);
            Assert.False(result.IsSuccess);
            Assert.Contains("one or more Vendor IDs", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateOperation_InvalidJson_FailsValidation()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var model = GetValidCreateModelWithFacility(Guid.NewGuid().ToString(), new List<string> { "Patient" });
            model.OperationJson = "invalid";

            var result = await operationManager.CreateOperation(model);
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateOperation_ValidUpdate_Succeeds()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            // Create first
            string facilityId = Guid.NewGuid().ToString();
            var createModel = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            var createResult = await operationManager.CreateOperation(createModel);
            var opId = ((OperationModel)createResult.ObjectResult).Id;

            // Update
            var updateModel = new UpdateOperationModel
            {
                Id = opId,
                FacilityId = facilityId,
                OperationJson = "{\"Name\": \"Updated Copy\", \"Description\": \"Updated Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
                ResourceTypes = new List<string> { "Patient", "Observation" },
                Name = "UpdatedName",
                Description = "Updated",
                IsDisabled = true
            };

            var updateResult = await operationManager.UpdateOperation(updateModel);

            Assert.True(updateResult.IsSuccess, updateResult.ErrorMessage);
            var updated = await operationQueries.Get(opId, facilityId);
            Assert.Equal("UpdatedName", updated.Name);
            Assert.Equal(2, updated.OperationResourceTypes.Count);
        }

        [Fact]
        public async Task UpdateOperation_ConvertFacilityToVendor_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            // Create with facility
            string facilityId = Guid.NewGuid().ToString();
            var createModel = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            var createResult = await operationManager.CreateOperation(createModel);
            var opId = ((OperationModel)createResult.ObjectResult).Id;

            // Update with vendors
            var updateModel = new UpdateOperationModel
            {
                Id = opId,
                VendorIds = new List<Guid> { Guid.NewGuid() },
                OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
                ResourceTypes = new List<string> { "Patient" }
            };

            var updateResult = await operationManager.UpdateOperation(updateModel);
            Assert.False(updateResult.IsSuccess);
            Assert.Contains("cannot also be a vendor operation", updateResult.ErrorMessage);
        }

        [Fact]
        public async Task DeleteOperation_ByFacility_DeletesSuccessfully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            string facilityId = Guid.NewGuid().ToString();
            var model = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            await operationManager.CreateOperation(model);

            var deleteModel = new DeleteOperationModel { FacilityId = facilityId };
            var result = await operationManager.DeleteOperation(deleteModel);

            Assert.True(result);

            var search = await operationQueries.Search(new OperationSearchModel { FacilityId = facilityId });
            Assert.Empty(search.Records);
        }

        [Fact]
        public async Task DeleteOperation_NonExisting_ReturnsFalse()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var deleteModel = new DeleteOperationModel { FacilityId = Guid.NewGuid().ToString() };
            var result = await operationManager.DeleteOperation(deleteModel);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateVendorPresetsForOperation_AddsVendors()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var vendorManager = scope.ServiceProvider.GetRequiredService<IVendorManager>();
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            // Create vendor
            var vendor = await vendorManager.CreateVendor(Guid.NewGuid().ToString());
            var vendorIds = new List<Guid> { vendor.Id };

            // Create operation with vendors
            var createModel = GetValidCreateModelWithVendors(vendorIds, new List<string> { "Patient" });
            var createResult = await operationManager.CreateOperation(createModel);
            var opId = ((OperationModel)createResult.ObjectResult).Id;

            // Add another vendor
            var newVendor = await vendorManager.CreateVendor(Guid.NewGuid().ToString());
            var newVendorIds = new List<Guid> { vendor.Id, newVendor.Id };
            await operationManager.UpdateVendorPresetsForOperation(opId, newVendorIds);

            var updated = await operationQueries.Get(opId, null);
            Assert.Equal(2, updated.VendorPresets.Count);
        }

        [Fact]
        public async Task UpdateOperationResourceTypesForOperation_WithStrings_Updates()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            string facilityId = Guid.NewGuid().ToString();
            var createModel = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            var createResult = await operationManager.CreateOperation(createModel);
            var opId = ((OperationModel)createResult.ObjectResult).Id;

            await operationManager.UpdateOperationResourceTypesForOperation(opId, new List<string> { "Patient", "Observation" });

            var updated = await operationQueries.Get(opId, facilityId);
            Assert.Equal(2, updated.OperationResourceTypes.Count);
        }

        [Fact]
        public async Task UpdateOperationResourceTypesForOperation_WithResources_Updates()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();
            var resourceQueries = scope.ServiceProvider.GetRequiredService<IResourceQueries>();

            string facilityId = Guid.NewGuid().ToString();
            var createModel = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            var createResult = await operationManager.CreateOperation(createModel);
            var opId = ((OperationModel)createResult.ObjectResult).Id;

            var patient = await resourceQueries.Get("Patient");
            var observation = await resourceQueries.Get("Observation");
            await operationManager.UpdateOperationResourceTypesForOperation(opId, new List<ResourceModel> { patient, observation });

            var updated = await operationQueries.Get(opId, facilityId);
            Assert.Equal(2, updated.OperationResourceTypes.Count);
        }

        [Fact]
        public async Task CreateOperationSequences_Valid_CreatesSequences()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var operationSequenceQueries = scope.ServiceProvider.GetRequiredService<IOperationSequenceQueries>();

            string facilityId = Guid.NewGuid().ToString();
            var createModel1 = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            var createResult1 = await operationManager.CreateOperation(createModel1);
            var opId1 = ((OperationModel)createResult1.ObjectResult).Id;

            var createModel2 = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            createModel2.Name = "Op2";
            var createResult2 = await operationManager.CreateOperation(createModel2);
            var opId2 = ((OperationModel)createResult2.ObjectResult).Id;

            var seqModel = new CreateOperationSequencesModel
            {
                FacilityId = facilityId,
                ResourceType = "Patient",
                OperationSequences = new List<CreateOperationSequenceModel>
                {
                    new() { OperationId = opId1, Sequence = 1 },
                    new() { OperationId = opId2, Sequence = 2 }
                }
            };

            var result = await operationManager.CreateOperationSequences(seqModel);

            Assert.Equal(2, result.Count);
            var searched = await operationSequenceQueries.Search(new OperationSequenceSearchModel { FacilityId = facilityId, ResourceType = "Patient" });
            Assert.Equal(2, searched.Count);
        }

        [Fact]
        public async Task CreateOperationSequences_DuplicateSequence_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var seqModel = new CreateOperationSequencesModel
            {
                FacilityId = Guid.NewGuid().ToString(),
                ResourceType = "Patient",
                OperationSequences = new List<CreateOperationSequenceModel>
                {
                    new() { OperationId = Guid.NewGuid(), Sequence = 1 },
                    new() { OperationId = Guid.NewGuid(), Sequence = 1 }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => operationManager.CreateOperationSequences(seqModel));
        }

        [Fact]
        public async Task DeleteOperationSequence_Existing_DeletesSuccessfully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var operationSequenceQueries = scope.ServiceProvider.GetRequiredService<IOperationSequenceQueries>();

            // Setup sequence
            string facilityId = Guid.NewGuid().ToString();
            var createModel = GetValidCreateModelWithFacility(facilityId, new List<string> { "Patient" });
            var createResult = await operationManager.CreateOperation(createModel);
            var opId = ((OperationModel)createResult.ObjectResult).Id;

            var seqModel = new CreateOperationSequencesModel
            {
                FacilityId = facilityId,
                ResourceType = "Patient",
                OperationSequences = new List<CreateOperationSequenceModel> { new() { OperationId = opId, Sequence = 1 } }
            };
            await operationManager.CreateOperationSequences(seqModel);

            var deleteModel = new DeleteOperationSequencesModel { FacilityId = facilityId, ResourceType = "Patient" };
            var result = await operationManager.DeleteOperationSequence(deleteModel);

            Assert.True(result);
            var searched = await operationSequenceQueries.Search(new OperationSequenceSearchModel { FacilityId = facilityId, ResourceType = "Patient" });
            Assert.Empty(searched);
        }

        [Fact]
        public async Task DeleteOperationSequence_NonExisting_ReturnsFalse()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var deleteModel = new DeleteOperationSequencesModel { FacilityId = Guid.NewGuid().ToString() };
            var result = await operationManager.DeleteOperationSequence(deleteModel);

            Assert.False(result);
        }
    }
}