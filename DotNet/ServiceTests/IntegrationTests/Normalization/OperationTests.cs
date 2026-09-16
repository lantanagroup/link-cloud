using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{
    [Collection("IntegrationTests")]
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
            var operationSequenceQueries = scope.ServiceProvider.GetRequiredService<IOperationSequenceQueries>();

            var vendorVersionId = Guid.NewGuid();

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
                VendorVersionIds = [vendorVersionId]
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
            Assert.Equal(vendorVersionId, sequences[0].VendorPresets[0].VendorVersion.Id);
            Assert.Equal("test", sequences[0].VendorPresets[0].VendorVersion.Version);
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

        private CreateOperationModel GetValidCreateModelWithVendorVersions(List<Guid> vendorVersionIds, List<string> resourceTypes)
        {
            return new CreateOperationModel
            {
            VendorVersionIds = vendorVersionIds,
                OperationType = "CopyProperty",
                OperationJson = "{\"Name\": \"Test Copy\", \"Description\": \"Test Copy Description\", \"SourceFhirPath\": \"id\", \"TargetFhirPath\": \"meta.versionId\"}",
                ResourceTypes = resourceTypes,
                Name = "TestOp",
                Description = "Test",
                IsDisabled = false
            };
        }

        [Theory]
        [InlineData(false, "active", true)]
        [InlineData(true, "active", true)]
        [InlineData(false, "inactive", false)]
        [InlineData(true, "inactive", false)]
        [InlineData(false, "unknown", false)]
        [InlineData(true, "unknown", false)]
        [InlineData(false, "cdc", false)]
        [InlineData(true, "cdc", false)]
        public async Task SaveHSLOCMap_RequiresActiveHSLOCCode(bool update, string codeKind, bool expectedSuccess)
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var queries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();
            var context = scope.ServiceProvider.GetRequiredService<NormalizationDbContext>();
            var prefix = Guid.NewGuid().ToString();
            context.HSLOCS.AddRange(
                new HSLOC { HSLOCCode = prefix + "active", CDCCode = prefix + "cdc", IsActive = true },
                new HSLOC { HSLOCCode = prefix + "inactive", IsActive = false });
            await context.SaveChangesAsync();

            var model = GetValidCreateModelWithFacility(Guid.NewGuid().ToString(), ["Location"]);
            model.OperationType = nameof(OperationType.HSLOCMap);
            model.OperationJson = JsonSerializer.Serialize(new HSLOCMapOperation([]));
            OperationModel? original = null;
            if (update)
            {
                var created = await manager.CreateOperation(model);
                Assert.True(created.IsSuccess, created.ErrorMessage);
                original = Assert.IsType<OperationModel>(created.ObjectResult);
            }

            model.OperationJson = JsonSerializer.Serialize(new HSLOCMapOperation(
                [new CodeSystemMap("urn:local", MappingTargetSystems.HslocUrl, new Dictionary<string, CodeMap>
                {
                    ["valid"] = new CodeMap(prefix + "active", "Active"),
                    ["candidate"] = new CodeMap(prefix + codeKind, "Candidate")
                })]));
            var result = update
                ? await manager.UpdateOperation(new UpdateOperationModel
                {
                    Id = original!.Id, FacilityId = model.FacilityId, Name = model.Name,
                    OperationJson = model.OperationJson, ResourceTypes = model.ResourceTypes
                })
                : await manager.CreateOperation(model);

            Assert.Equal(expectedSuccess, result.IsSuccess);
            if (!expectedSuccess)
            {
                Assert.Contains(prefix + codeKind, result.ErrorMessage);
                var saved = await queries.Search(new OperationSearchModel { FacilityId = model.FacilityId });
                if (update)
                    Assert.Equal(original!.OperationJson, Assert.Single(saved.Records).OperationJson);
                else
                    Assert.Empty(saved.Records);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreateOperation_DuplicateHSLOCMap_IsRejected(bool disabled)
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var queries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();
            var model = GetValidCreateModelWithFacility(Guid.NewGuid().ToString(), ["Location"]);
            model.OperationType = OperationType.HSLOCMap.ToString();
            model.OperationJson = JsonSerializer.Serialize(new HSLOCMapOperation([]));
            model.IsDisabled = disabled;

            var first = await manager.CreateOperation(model);
            Assert.True(first.IsSuccess, first.ErrorMessage);

            model.IsDisabled = false;
            var duplicate = await manager.CreateOperation(model);
            Assert.False(duplicate.IsSuccess);
            Assert.Equal("Only one HSLOC Map operation is allowed per facility.", duplicate.ErrorMessage);
            var records = await queries.Search(new OperationSearchModel { FacilityId = model.FacilityId, IncludeDisabled = true });
            Assert.Single(records.Records);

            model.FacilityId = Guid.NewGuid().ToString();
            var otherFacility = await manager.CreateOperation(model);
            Assert.True(otherFacility.IsSuccess, otherFacility.ErrorMessage);
        }

        [Fact]
        public async Task UpdateOperation_HSLOCMap_AllowsSelfButRejectsOccupiedFacility()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOperationManager>();
            var queries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();
            var model = GetValidCreateModelWithFacility(Guid.NewGuid().ToString(), ["Location"]);
            model.OperationType = OperationType.HSLOCMap.ToString();
            model.OperationJson = JsonSerializer.Serialize(new HSLOCMapOperation([]));
            var first = await manager.CreateOperation(model);
            Assert.True(first.IsSuccess, first.ErrorMessage);
            var original = Assert.IsType<OperationModel>(first.ObjectResult);

            var update = new UpdateOperationModel
            {
                Id = original.Id,
                FacilityId = model.FacilityId,
                OperationJson = model.OperationJson,
                ResourceTypes = model.ResourceTypes,
                Name = "Updated HSLOC map",
                IsDisabled = true
            };
            var selfUpdate = await manager.UpdateOperation(update);
            Assert.True(selfUpdate.IsSuccess, selfUpdate.ErrorMessage);

            model.FacilityId = Guid.NewGuid().ToString();
            var second = await manager.CreateOperation(model);
            Assert.True(second.IsSuccess, second.ErrorMessage);

            update.FacilityId = model.FacilityId;
            var duplicate = await manager.UpdateOperation(update);
            Assert.False(duplicate.IsSuccess);
            Assert.Equal("Only one HSLOC Map operation is allowed per facility.", duplicate.ErrorMessage);
            var unchanged = await queries.Get(original.Id, original.FacilityId);
            Assert.Equal(original.FacilityId, unchanged.FacilityId);
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
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            var vendorVersionId = Guid.NewGuid();
            var model = GetValidCreateModelWithVendorVersions([vendorVersionId], new List<string> { "Patient" });

            var result = await operationManager.CreateOperation(model);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.ObjectResult);

            var created = (OperationModel)result.ObjectResult;
            var searched = await operationQueries.Search(new OperationSearchModel { VendorVersionId = vendorVersionId });
            Assert.Contains(searched.Records, o => o.Id == created.Id);
        }

        [Fact]
        public async Task CreateOperation_WithBothFacilityAndVendors_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var operationManager = scope.ServiceProvider.GetRequiredService<IOperationManager>();

            var model = GetValidCreateModelWithFacility(Guid.NewGuid().ToString(), new List<string> { "Patient" });
            model.VendorVersionIds = new List<Guid> { Guid.NewGuid() };

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
            Assert.Contains("one or more Vendor Version IDs", result.ErrorMessage);
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
                VendorVersionIds = new List<Guid> { Guid.NewGuid() },
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
            var operationQueries = scope.ServiceProvider.GetRequiredService<IOperationQueries>();

            var vendorVersionId = Guid.NewGuid();

            var createModel = GetValidCreateModelWithVendorVersions([vendorVersionId], new List<string> { "Patient" });
            var createResult = await operationManager.CreateOperation(createModel);
            var opId = ((OperationModel)createResult.ObjectResult).Id;

            var newVendorVersionId = Guid.NewGuid();
            await operationManager.UpdateVendorPresetsForOperation(opId, [vendorVersionId, newVendorVersionId]);

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
            operationSequenceQueries.ClearCache(new OperationSequenceSearchModel { ResourceTypeId = opId, FacilityId = facilityId, ResourceType = "Patient" });
            var searched = await operationSequenceQueries.Search(new OperationSequenceSearchModel { ResourceTypeId = opId, FacilityId = facilityId, ResourceType = "Patient" });
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