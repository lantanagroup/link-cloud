using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using ServiceTests.IntegrationTests.Normalization;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.IntegrationTests.Normalization
{
    [Collection("NormalizationIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class NormalizationOperationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly NormalizationIntegrationTestFixture _fixture;
        private readonly IDatabase _database;
        private readonly IOperationManager _operationManager;
        private readonly IOperationQueries _operationQueries;
        private readonly CopyPropertyOperationService _copyOperationService;
        private readonly CodeMapOperationService _codeMapOperationService;
        private readonly ConditionalTransformOperationService _conditionalTransformService;

        public NormalizationOperationTests(NormalizationIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            _operationManager = _fixture.ServiceProvider.GetRequiredService<IOperationManager>();
            _operationQueries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            _copyOperationService = _fixture.ServiceProvider.GetRequiredService<CopyPropertyOperationService>();
            _codeMapOperationService = _fixture.ServiceProvider.GetRequiredService<CodeMapOperationService>();
            _conditionalTransformService = _fixture.ServiceProvider.GetRequiredService<ConditionalTransformOperationService>();
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Location_Identifier_To_Type_Create_TargetElement()
        {
            var operation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Location"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string locationPath = Path.Combine(assemblyLocation, "Resources", "Location.txt");
            string location_text = File.ReadAllText(locationPath);
            var location = parser.Parse<Location>(location_text);

            if (location == null)
            {
                Assert.Fail("No location resource found");
            }

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, location);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedLocation = (Location)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(location_text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedLocation));

            Assert.Equal(location.Identifier[0].Value, modifiedLocation.Type[0].Coding[0].Code);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Location_Identifier_To_Type_Update_TargetElement()
        {
            var operation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Location"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string locationPath = Path.Combine(assemblyLocation, "Resources", "LocationWithCodeSection.txt");
            string location_text = File.ReadAllText(locationPath);
            var location = parser.Parse<Location>(location_text);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, location);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedLocation = (Location)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(location_text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedLocation));

            Assert.Equal(location.Identifier[0].Value, modifiedLocation.Type[0].Coding[0].Code);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Patient_Identifier_To_Family_Update_TargetElement()
        {
            var operation = new CopyPropertyOperation("Copy Patient Identifier to Family Name", "identifier[0].value", "name[0].family");

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Patient"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Patient.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Patient>(text);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Patient)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(resource.Identifier[0].Value, modifiedResource.Name[0].Family);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Observation_ValueToCodeText_Update_TargetElement()
        {
            var operation = new CopyPropertyOperation("Copy Observation Value to Code Text", "valueQuantity.value", "code.text");

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Observation"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Observation.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Observation>(text);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Observation)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotNull(modifiedResource.Code);
            Assert.NotNull(modifiedResource.Code.Text);
            Assert.Equal(resource.Value.First().Value.ToString(), modifiedResource.Code.Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Patient_NameToText_CreateTarget()
        {
            var operation = new CopyPropertyOperation(
                "Copy Patient Given Name to Name Text",
                "name[0].given[0]",
                "name[0].text"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Patient Name to Text",
                IsDisabled = false,
                ResourceTypes = ["Patient"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.NotEqual(default, result.Id);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.NotEqual(default, fetched.Id);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Patient.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Patient>(text);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Patient)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotEmpty(modifiedResource.Name);
            Assert.NotNull(modifiedResource.Name[0].Text);
            Assert.Equal("John", modifiedResource.Name[0].Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_MedicationRequest_DosageToNote_CreateTarget()
        {
            var operation = new CopyPropertyOperation(
                "Copy MedicationRequest Dosage to Note",
                "dosageInstruction[0].doseAndRate[0].dose.value",
                "note[0].text"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy MedicationRequest Dosage to Note",
                IsDisabled = false,
                ResourceTypes = ["MedicationRequest"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.NotEqual(default, result.Id);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.NotEqual(default, fetched.Id);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "MedicationRequest.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<MedicationRequest>(text);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (MedicationRequest)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotEmpty(modifiedResource.Note);
            Assert.Equal(1, modifiedResource.Note.Count);
            var note = modifiedResource.Note[0];
            Assert.NotNull(note.Text);
            Assert.Equal("325", note.Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Condition_OnsetToCode_UpdateTarget()
        {
            var operation = new CopyPropertyOperation(
                "Copy Condition Onset to Code Text",
                "onsetDateTime",
                "code.text"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Condition Onset to Code Text",
                IsDisabled = false,
                ResourceTypes = ["Condition"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.NotEqual(default, result.Id);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.NotEqual(default, fetched.Id);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Condition.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Condition>(text);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Condition)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotNull(modifiedResource.Code);
            Assert.NotNull(modifiedResource.Code.Text);
            Assert.Equal("2023-05-01T10:00:00Z", modifiedResource.Code.Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Encounter_PeriodStartToReason()
        {
            var operation = new CopyPropertyOperation(
                "Copy Encounter Period Start to Reason",
                "period.start",
                "reasonCode[0].text"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "EncounterCopyTest.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            Assert.NotNull(resource.Period);
            Assert.NotNull(resource.Period.StartElement);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotEmpty(modifiedResource.ReasonCode);
            Assert.NotNull(modifiedResource.ReasonCode[0].Text);
            Assert.Equal(resource.Period.StartElement.ToString(), modifiedResource.ReasonCode[0].Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Patient_BirthDateToName()
        {
            var operation = new CopyPropertyOperation(
                "Copy Patient BirthDate to Name",
                "birthDate",
                "name[0].text"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Patient"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "PatientBirthDateTest.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Patient>(text);

            Assert.NotNull(resource.BirthDateElement);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Patient)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotEmpty(modifiedResource.Name);
            Assert.NotNull(modifiedResource.Name[0].Text);
            Assert.Equal(resource.BirthDateElement.ToString(), modifiedResource.Name[0].Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_MedicationRequest_AuthoredOnToNote()
        {
            var operation = new CopyPropertyOperation(
                "Copy MedicationRequest AuthoredOn to Note",
                "authoredOn",
                "note[0].text"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["MedicationRequest"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "MedicationRequestAuthoredOnToNote.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<MedicationRequest>(text);

            Assert.NotNull(resource.AuthoredOnElement);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (MedicationRequest)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotEmpty(modifiedResource.Note);
            Assert.NotNull(modifiedResource.Note[0].Text);
            Assert.Equal(resource.AuthoredOnElement.ToString(), modifiedResource.Note[0].Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_AllergyIntolerance_ClinicalStatusToReaction()
        {
            var operation = new CopyPropertyOperation(
                "Copy AllergyIntolerance ClinicalStatus to Reaction",
                "clinicalStatus.coding[0].code",
                "reaction[0].description"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["AllergyIntolerance"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "AllergyIntolerance.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<AllergyIntolerance>(text);

            Assert.NotNull(resource.ClinicalStatus);
            Assert.NotEmpty(resource.ClinicalStatus.Coding);
            Assert.NotNull(resource.ClinicalStatus.Coding[0].Code);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (AllergyIntolerance)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotEmpty(modifiedResource.Reaction);
            Assert.NotNull(modifiedResource.Reaction[0].Description);
            Assert.Equal(resource.ClinicalStatus.Coding[0].Code, modifiedResource.Reaction[0].Description);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_DiagnosticReport_EffectiveDateTimeToCode()
        {
            var operation = new CopyPropertyOperation(
                "Copy DiagnosticReport EffectiveDateTime to Code",
                "effectiveDateTime",
                "code.text"
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["DiagnosticReport"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "DiagnosticReport.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<DiagnosticReport>(text);

            Assert.NotNull(resource.Effective);
            Assert.True(resource.Effective is FhirDateTime, "The DiagnosticReport resource effective field must be a FhirDateTime.");
            var effectiveDateTime = resource.Effective as FhirDateTime;
            Assert.NotNull(effectiveDateTime.Value);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            var operationResult = await _copyOperationService.EnqueueOperationAsync(copyOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (DiagnosticReport)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.NotNull(modifiedResource.Code);
            Assert.NotNull(modifiedResource.Code.Text);
            Assert.Equal(((FhirDateTime)resource.Effective).ToString(), modifiedResource.Code.Text);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_MultipleOperations_Queue()
        {
            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Patient.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Patient>(text);

            var operations = new List<CopyPropertyOperation>
            {
                new CopyPropertyOperation(
                    "Copy Given Name to Name Text",
                    "name[0].given[0]",
                    "name[0].text"
                ),
                new CopyPropertyOperation(
                    "Copy Family Name to Identifier",
                    "name[0].family",
                    "identifier[1].value"
                ),
                new CopyPropertyOperation(
                    "Copy Identifier to Name Family",
                    "identifier[0].value",
                    "name[0].family"
                )
            };

            var operationIds = new List<Guid>();
            foreach (var op in operations)
            {
                var taskResult = await _operationManager.CreateOperation(new CreateOperationModel()
                {
                    OperationJson = JsonSerializer.Serialize(op),
                    OperationType = OperationType.CopyProperty.ToString(),
                    FacilityId = "TestFacilityId",
                    Description = $"Integration Test Multiple Operations - {op.Name}",
                    IsDisabled = false,
                    ResourceTypes = ["Patient"]
                });

                Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
                Assert.NotNull(taskResult.ObjectResult);

                var result = (OperationModel)taskResult.ObjectResult;

                Assert.NotNull(result);
                Assert.True(result.Id != default);
                operationIds.Add(result.Id);
            }

            var tasks = new List<Task<OperationResult>>();
            var fetchedOperations = new List<CopyPropertyOperation>();
            foreach (var id in operationIds)
            {
                var fetched = await _operationQueries.Get(id, "TestFacilityId");
                Assert.NotNull(fetched);
                Assert.True(fetched.Id != default);

                Assert.NotNull(fetched.OperationJson);
                var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

                Assert.NotNull(copyOperation);
                Assert.NotNull(copyOperation.SourceFhirPath);
                Assert.NotNull(copyOperation.TargetFhirPath);

                fetchedOperations.Add(copyOperation);
                tasks.Add(_copyOperationService.EnqueueOperationAsync(copyOperation, resource));
            }

            var results = await Task.WhenAll(tasks);

            for (int i = 0; i < results.Length; i++)
            {
                var operationResult = results[i];
                Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

                var modifiedResource = (Patient)operationResult.Resource;
                var operation = fetchedOperations[i];

                _output.WriteLine($"Modified Resource for operation '{operation.Name}': ");
                FhirJsonSerializer serializer = new FhirJsonSerializer();
                _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

                if (operation.Name == "Copy Given Name to Name Text")
                {
                    Assert.NotEmpty(modifiedResource.Name);
                    Assert.NotNull(modifiedResource.Name[0].Text);
                    Assert.Equal("John", modifiedResource.Name[0].Text);
                }
                else if (operation.Name == "Copy Family Name to Identifier")
                {
                    Assert.True(modifiedResource.Identifier.Count >= 2);
                    Assert.NotNull(modifiedResource.Identifier[1].Value);
                    Assert.Equal("Smith", modifiedResource.Identifier[1].Value);
                }
                else if (operation.Name == "Copy Identifier to Name Family")
                {
                    Assert.NotEmpty(modifiedResource.Name);
                    Assert.NotNull(modifiedResource.Name[0].Family);
                    Assert.Equal(modifiedResource.Identifier[0].Value, modifiedResource.Name[0].Family);
                }
            }
        }


        [Fact]
        public async Task Integration_CodeMapOperation_Encounter_Class_Maps_Coding()
        {
            // Arrange: Define a code mapping operation for Encounter.class
            var codeMaps = new Dictionary<string, CodeMap>
            {
                { "AMB", new CodeMap("ambulatory", "Ambulatory Care") }
            };
            var codeSystemMap = new CodeSystemMap(
                sourceSystem: "http://hl7.org/fhir/v3/ActCode",
                targetSystem: "http://example.org/codes",
                codeMaps: codeMaps
            );
            var operation = new CodeMapOperation(
                name: "Map Encounter Class",
                fhirPath: "class",
                codeSystemMaps: new List<CodeSystemMap> { codeSystemMap }
            );

            // Create operation in the system
            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CodeMap.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Code Map Operation - Encounter Class",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            // Fetch the created operation
            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var codeMapOperation = JsonSerializer.Deserialize<CodeMapOperation>(fetched.OperationJson);
            Assert.NotNull(codeMapOperation);
            Assert.NotNull(codeMapOperation.FhirPath);
            Assert.NotEmpty(codeMapOperation.CodeSystemMaps);

            // Load Encounter resource
            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string encounterPath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string encounterText = File.ReadAllText(encounterPath);
            var encounter = parser.Parse<Encounter>(encounterText);

            if (encounter == null)
            {
                Assert.Fail("No encounter resource found");
            }
            // Act: Execute the operation
            var operationResult = await _codeMapOperationService.EnqueueOperationAsync(codeMapOperation, encounter);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            // Assert: Verify the mapping
            var modifiedEncounter = (Encounter)operationResult.Resource;
            Assert.NotNull(modifiedEncounter.Class);

            _output.WriteLine("Original: ");
            _output.WriteLine(encounterText);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedEncounter));

            Assert.Equal("http://example.org/codes", modifiedEncounter.Class.System);
            Assert.Equal("ambulatory", modifiedEncounter.Class.Code);
            Assert.Equal("Ambulatory Care", modifiedEncounter.Class.Display);
        }

        [Fact]
        public async Task Integration_CodeMapOperation_Encounter_Type_Maps_CodeableConcept()
        {
            // Arrange: Define a code mapping operation for Encounter.type
            var codeMaps = new Dictionary<string, CodeMap>
            {
                { "99201", new CodeMap("office-visit", "Office Visit") }
            };
            var codeSystemMap = new CodeSystemMap(
                sourceSystem: "http://www.ama-assn.org/go/cpt",
                targetSystem: "http://example.org/visit-types",
                codeMaps: codeMaps
            );
            var operation = new CodeMapOperation(
                name: "Map Encounter Type",
                fhirPath: "type",
                codeSystemMaps: new List<CodeSystemMap> { codeSystemMap }
            );

            // Create operation in the system
            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CodeMap.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Code Map Operation - Encounter Type",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            // Fetch the created operation
            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var codeMapOperation = JsonSerializer.Deserialize<CodeMapOperation>(fetched.OperationJson);
            Assert.NotNull(codeMapOperation);
            Assert.NotNull(codeMapOperation.FhirPath);
            Assert.NotEmpty(codeMapOperation.CodeSystemMaps);

            // Load Encounter resource
            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string encounterPath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string encounterText = File.ReadAllText(encounterPath);
            var encounter = parser.Parse<Encounter>(encounterText);

            if (encounter == null)
            {
                Assert.Fail("No encounter resource found");
            }

            // Act: Execute the operation
            var operationResult = await _codeMapOperationService.EnqueueOperationAsync(codeMapOperation, encounter);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            // Assert: Verify the mapping
            var modifiedEncounter = (Encounter)operationResult.Resource;
            Assert.NotEmpty(modifiedEncounter.Type);
            var mappedCoding = modifiedEncounter.Type[0].Coding.FirstOrDefault(c => c.System == "http://example.org/visit-types");

            _output.WriteLine("Original: ");
            _output.WriteLine(encounterText);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedEncounter));

            Assert.NotNull(mappedCoding);
            Assert.Equal("http://example.org/visit-types", mappedCoding.System);
            Assert.Equal("office-visit", mappedCoding.Code);
            Assert.Equal("Office Visit", mappedCoding.Display);
        }

        [Fact]
        public async Task Integration_CodeMapOperation_Encounter_Class_NoMatchingMap()
        {
            // Arrange: Define a code mapping operation with no matching system
            var codeMaps = new Dictionary<string, CodeMap>
            {
                { "INPATIENT", new CodeMap("inpatient", "Inpatient Care") }
            };
            var codeSystemMap = new CodeSystemMap(
                sourceSystem: "http://example.org/non-matching-system",
                targetSystem: "http://example.org/codes",
                codeMaps: codeMaps
            );
            var operation = new CodeMapOperation(
                name: "Map Encounter Class No Match",
                fhirPath: "class",
                codeSystemMaps: new List<CodeSystemMap> { codeSystemMap }
            );

            // Create operation in the system
            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CodeMap.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Code Map Operation - No Matching Map",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            // Fetch the created operation
            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var codeMapOperation = JsonSerializer.Deserialize<CodeMapOperation>(fetched.OperationJson);
            Assert.NotNull(codeMapOperation);
            Assert.NotNull(codeMapOperation.FhirPath);
            Assert.NotEmpty(codeMapOperation.CodeSystemMaps);

            // Load Encounter resource
            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string encounterPath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string encounterText = File.ReadAllText(encounterPath);
            var encounter = parser.Parse<Encounter>(encounterText);

            if (encounter == null)
            {
                Assert.Fail("No encounter resource found");
            }

            // Store original class values for comparison
            var originalClass = encounter.Class.DeepCopy() as Coding;

            // Act: Execute the operation
            var operationResult = await _codeMapOperationService.EnqueueOperationAsync(codeMapOperation, encounter);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            // Assert: Verify no changes were made
            var modifiedEncounter = (Encounter)operationResult.Resource;
            Assert.NotNull(modifiedEncounter.Class);

            _output.WriteLine("Original: ");
            _output.WriteLine(encounterText);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedEncounter));

            Assert.Equal(originalClass.System, modifiedEncounter.Class.System);
            Assert.Equal(originalClass.Code, modifiedEncounter.Class.Code);
            Assert.Equal(originalClass.Display, modifiedEncounter.Class.Display);
        }

        [Fact]
        public async Task Integration_CodeMapOperation_Observation_Code_Maps_CodeableConcept()
        {
            // Arrange: Define a code mapping operation for Observation.code
            var codeMaps = new Dictionary<string, CodeMap>
            {
                { "8310-5", new CodeMap("body-temp", "Body Temperature Standard") }
            };
            var codeSystemMap = new CodeSystemMap(
                sourceSystem: "http://loinc.org",
                targetSystem: "http://example.org/codes",
                codeMaps: codeMaps
            );
            var operation = new CodeMapOperation(
                name: "Map Observation Code",
                fhirPath: "code",
                codeSystemMaps: new List<CodeSystemMap> { codeSystemMap }
            );

            // Create operation in the system
            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CodeMap.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Code Map Operation - Observation Code",
                IsDisabled = false,
                ResourceTypes = ["Observation"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            // Fetch the created operation
            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var codeMapOperation = JsonSerializer.Deserialize<CodeMapOperation>(fetched.OperationJson);
            Assert.NotNull(codeMapOperation);
            Assert.NotNull(codeMapOperation.FhirPath);
            Assert.NotEmpty(codeMapOperation.CodeSystemMaps);

            // Load Observation resource
            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string observationPath = Path.Combine(assemblyLocation, "Resources", "BodyTempObservation.txt");
            string observationText = File.ReadAllText(observationPath);
            var observation = parser.Parse<Observation>(observationText);

            if (observation == null)
            {
                Assert.Fail("No observation resource found");
            }

            // Act: Execute the operation
            var operationResult = await _codeMapOperationService.EnqueueOperationAsync(codeMapOperation, observation);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            // Assert: Verify the mapping
            var modifiedObservation = (Observation)operationResult.Resource;
            Assert.NotNull(modifiedObservation.Code);
            var mappedCoding = modifiedObservation.Code.Coding.FirstOrDefault(c => c.System == "http://example.org/codes");

            _output.WriteLine("Original: ");
            _output.WriteLine(observationText);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedObservation));

            Assert.NotNull(mappedCoding);
            Assert.Equal("http://example.org/codes", mappedCoding.System);
            Assert.Equal("body-temp", mappedCoding.Code);
            Assert.Equal("Body Temperature Standard", mappedCoding.Display);
        }

        [Fact]
        public async Task Integration_CodeMapOperation_Condition_Code_Maps_CodeableConcept()
        {
            // Arrange: Define a code mapping operation for Condition.code
            var codeMaps = new Dictionary<string, CodeMap>
            {
                { "44054006", new CodeMap("diabetes", "Diabetes Mellitus") }
            };
            var codeSystemMap = new CodeSystemMap(
                sourceSystem: "http://snomed.info/sct",
                targetSystem: "http://example.org/conditions",
                codeMaps: codeMaps
            );

            var operation = new CodeMapOperation(
                name: "Map Condition Code",
                fhirPath: "code",
                codeSystemMaps: new List<CodeSystemMap> { codeSystemMap }
            );

            // Create operation in the system
            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CodeMap.ToString(),
                FacilityId = "TestFacilityId",
                Description = "Integration Test Code Map Operation - Condition Code",
                IsDisabled = false,
                ResourceTypes = ["Condition"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            // Fetch the created operation
            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var codeMapOperation = JsonSerializer.Deserialize<CodeMapOperation>(fetched.OperationJson);
            Assert.NotNull(codeMapOperation);
            Assert.NotNull(codeMapOperation.FhirPath);
            Assert.NotEmpty(codeMapOperation.CodeSystemMaps);

            // Load Condition resource
            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string conditionPath = Path.Combine(assemblyLocation, "Resources", "DiabetesCondition.txt");
            string conditionText = File.ReadAllText(conditionPath);
            var condition = parser.Parse<Condition>(conditionText);

            if (condition == null)
            {
                Assert.Fail("No condition resource found");
            }

            // Act: Execute the operation
            var operationResult = await _codeMapOperationService.EnqueueOperationAsync(codeMapOperation, condition);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            // Assert: Verify the mapping
            var modifiedCondition = (Condition)operationResult.Resource;
            Assert.NotNull(modifiedCondition.Code);
            var mappedCoding = modifiedCondition.Code.Coding.FirstOrDefault(c => c.System == "http://example.org/conditions");

            _output.WriteLine("Original: ");
            _output.WriteLine(conditionText);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedCondition));

            Assert.NotNull(mappedCoding);
            Assert.Equal("http://example.org/conditions", mappedCoding.System);
            Assert.Equal("diabetes", mappedCoding.Code);
            Assert.Equal("Diabetes Mellitus", mappedCoding.Display);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_Equal_Positive()
        {
            var condition = new TransformCondition("class.code", ConditionOperator.Equal, "AMB");
            var operation = new ConditionalTransformOperation(
                "Set Status if Class Code is AMB",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - Equal Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            if (operationResult.SuccessCode != OperationStatus.Success)
            {
                _output.WriteLine(operationResult.ErrorMessage);
            }
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_Equal_Negative()
        {
            var condition = new TransformCondition("class.code", ConditionOperator.Equal, "INPATIENT");
            var operation = new ConditionalTransformOperation(
                "Set Status if Class Code is INPATIENT",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - Equal Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_NotEqual_Positive()
        {
            var condition = new TransformCondition("class.code", ConditionOperator.NotEqual, "INPATIENT");
            var operation = new ConditionalTransformOperation(
                "Set Status if Class Code is Not INPATIENT",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - NotEqual Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_NotEqual_Negative()
        {
            var condition = new TransformCondition("class.code", ConditionOperator.NotEqual, "AMB");
            var operation = new ConditionalTransformOperation(
                "Set Status if Class Code is Not AMB",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - NotEqual Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_GreaterThan_Positive()
        {
            var condition = new TransformCondition("period.start", ConditionOperator.GreaterThan, "2024-01-01");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period Start After 2024-01-01",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - GreaterThan Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_GreaterThan_Negative()
        {
            var condition = new TransformCondition("period.start", ConditionOperator.GreaterThan, "2025-01-01");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period Start After 2025-01-01",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - GreaterThan Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_GreaterThanOrEqual_Positive()
        {
            var condition = new TransformCondition("period.start", ConditionOperator.GreaterThanOrEqual, "2024-12-01");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period Start On or After 2024-12-01",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - GreaterThanOrEqual Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_GreaterThanOrEqual_Negative()
        {
            var condition = new TransformCondition("period.start", ConditionOperator.GreaterThanOrEqual, "2025-01-01");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period Start On or After 2025-01-01",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - GreaterThanOrEqual Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_LessThan_Positive()
        {
            var condition = new TransformCondition("period.end", ConditionOperator.LessThan, "2025-01-01");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period End Before 2025-01-01",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - LessThan Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_LessThan_Negative()
        {
            var condition = new TransformCondition("period.end", ConditionOperator.LessThan, "2024-01-01");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period End Before 2024-01-01",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - LessThan Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_LessThanOrEqual_Positive()
        {
            var condition = new TransformCondition("period.end", ConditionOperator.LessThanOrEqual, "2024-12-30");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period End On or Before 2024-12-30",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - LessThanOrEqual Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_LessThanOrEqual_Negative()
        {
            var condition = new TransformCondition("period.end", ConditionOperator.LessThanOrEqual, "2024-01-01");
            var operation = new ConditionalTransformOperation(
                "Set Status if Period End On or Before 2024-01-01",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - LessThanOrEqual Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_Exists_Positive()
        {
            var condition = new TransformCondition("period.end", ConditionOperator.Exists);
            var operation = new ConditionalTransformOperation(
                "Set Status if Period End Exists",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - Exists Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_Exists_Negative()
        {
            var condition = new TransformCondition("priority", ConditionOperator.Exists);
            var operation = new ConditionalTransformOperation(
                "Set Status if Priority Exists",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - Exists Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_NotExists_Positive()
        {
            var condition = new TransformCondition("priority", ConditionOperator.NotExists);
            var operation = new ConditionalTransformOperation(
                "Set Status if Priority Does Not Exist",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - NotExists Positive",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.Finished, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_NotExists_Negative()
        {
            var condition = new TransformCondition("period.end", ConditionOperator.NotExists);
            var operation = new ConditionalTransformOperation(
                "Set Status if Period End Does Not Exist",
                "status",
                Encounter.EncounterStatus.Finished,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - NotExists Negative",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "Encounter.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            
            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Encounter)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(Encounter.EncounterStatus.InProgress, modifiedResource.Status);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_Numeric_Equal_Positive()
        {
            var condition = new TransformCondition("valueQuantity.value", ConditionOperator.Equal, "98.6");
            var operation = new ConditionalTransformOperation(
                "Set Status if Value Equals 98.6",
                "status",
                ObservationStatus.Final,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - Numeric Equal Positive",
                IsDisabled = false,
                ResourceTypes = ["Observation"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "ConditionalObservation.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Observation>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);
            Assert.Equal(OperationStatus.Success, operationResult.SuccessCode);

            var modifiedResource = (Observation)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(ObservationStatus.Final, modifiedResource.Status.Value);
        }

        [Fact]
        public async Task Integration_ConditionalTransform_Numeric_Equal_Negative()
        {
            var condition = new TransformCondition("valueQuantity.value", ConditionOperator.Equal, "100.0");
            var operation = new ConditionalTransformOperation(
                "Set Status if Value Equals 100.0",
                "status",
                ObservationStatus.Final,
                new List<TransformCondition> { condition }
            );

            var taskResult = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = "ConditionalTransform",
                FacilityId = "TestFacilityId",
                Description = "Integration Test Conditional Transform - Numeric Equal Negative",
                IsDisabled = false,
                ResourceTypes = ["Observation"]
            });

            Assert.True(taskResult.IsSuccess, taskResult.ErrorMessage);
            Assert.NotNull(taskResult.ObjectResult);

            var result = (OperationModel)taskResult.ObjectResult;

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id, result.FacilityId);
            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);
            Assert.NotNull(fetched.OperationJson);

            var transformOperation = JsonSerializer.Deserialize<ConditionalTransformOperation>(fetched.OperationJson);
            Assert.NotNull(transformOperation);
            Assert.NotNull(transformOperation.TargetFhirPath);
            Assert.NotEmpty(transformOperation.Conditions);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "ConditionalObservation.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Observation>(text);

            var operationResult = await _conditionalTransformService.EnqueueOperationAsync(transformOperation, resource);

            Assert.Equal(OperationStatus.NoAction, operationResult.SuccessCode);
            Assert.Contains("Condition was not met", operationResult.ErrorMessage);

            var modifiedResource = (Observation)operationResult.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            var serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedResource));

            Assert.Equal(ObservationStatus.Preliminary, modifiedResource.Status.Value);
        }
    }
}