using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace NormalizationTests
{
    [Collection("IntegrationTest")]
    public class NormalizationOperationTests : IClassFixture<IntegrationTestFixture>
    {
        private readonly ITestOutputHelper _output;
        private readonly IntegrationTestFixture _fixture;
        private readonly IDatabase _database;
        private readonly IOperationManager _operationManager;
        private readonly IOperationQueries _operationQueries;
        private readonly CopyPropertyOperationService _operationService;

        public NormalizationOperationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            _operationManager = _fixture.ServiceProvider.GetRequiredService<IOperationManager>();
            _operationQueries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            _operationService = _fixture.ServiceProvider.GetRequiredService<CopyPropertyOperationService>();
        }

        [Fact]
        public async Task Unit_Location_Identifier_To_Type()
        {
            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string locationPath = Path.Combine(assemblyLocation, "Resources", "Location.txt");
            string location_text = File.ReadAllText(locationPath);
            var location = parser.Parse<Location>(location_text);

            CopyPropertyOperation copyOperation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            var result = await _operationService.EnqueueOperationAsync(copyOperation, location);
            Assert.Equal(OperationStatus.Success, result.SuccessCode);

            var modifiedLocation = (Location)result.Resource;

            _output.WriteLine("Original: ");
            _output.WriteLine(location_text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(modifiedLocation));

            Assert.Equal(location.Identifier[0].Value, modifiedLocation.Type[0].Coding[0].Code);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Location_Identifier_To_Type_Create_TargetElement()
        {
            var operation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize<object>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Location"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, location);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize<object>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Location"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, location);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize<object>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Patient"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize<object>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Observation"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize<CopyPropertyOperation>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Patient Name to Text",
                IsDisabled = false,
                ResourceTypes = ["Patient"]
            });

            Assert.NotNull(result);
            Assert.NotEqual(default(Guid), result.Id);

            var fetched = await _operationQueries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.NotEqual(default(Guid), fetched.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize<object>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy MedicationRequest Dosage to Note",
                IsDisabled = false,
                ResourceTypes = ["MedicationRequest"]
            });

            Assert.NotNull(result);
            Assert.NotEqual(default(Guid), result.Id);

            var fetched = await _operationQueries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.NotEqual(default(Guid), fetched.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize<object>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Condition Onset to Code Text",
                IsDisabled = false,
                ResourceTypes = ["Condition"]
            });

            Assert.NotNull(result);
            Assert.NotEqual(default(Guid), result.Id);

            var fetched = await _operationQueries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.NotEqual(default(Guid), fetched.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Encounter"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["Patient"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["MedicationRequest"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["AllergyIntolerance"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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

            var result = await _operationManager.CreateOperation(new CreateOperationModel()
            {
                OperationJson = JsonSerializer.Serialize(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Property Operation",
                IsDisabled = false,
                ResourceTypes = ["DiagnosticReport"]
            });

            Assert.NotNull(result);
            Assert.True(result.Id != default);

            var fetched = await _operationQueries.Get(result.Id);

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

            var operationResult = await _operationService.EnqueueOperationAsync(copyOperation, resource);
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
                var result = await _operationManager.CreateOperation(new CreateOperationModel()
                {
                    OperationJson = JsonSerializer.Serialize<object>(op),
                    OperationType = OperationType.CopyProperty.ToString(),
                    FacilityId = null,
                    Description = $"Integration Test Multiple Operations - {op.Name}",
                    IsDisabled = false,
                    ResourceTypes = ["Patient"]
                });

                Assert.NotNull(result);
                Assert.True(result.Id != default);
                operationIds.Add(result.Id);
            }

            var tasks = new List<Task<OperationResult>>();
            var fetchedOperations = new List<CopyPropertyOperation>();
            foreach (var id in operationIds)
            {
                var fetched = await _operationQueries.Get(id);
                Assert.NotNull(fetched);
                Assert.True(fetched.Id != default);

                Assert.NotNull(fetched.OperationJson);
                var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

                Assert.NotNull(copyOperation);
                Assert.NotNull(copyOperation.SourceFhirPath);
                Assert.NotNull(copyOperation.TargetFhirPath);

                fetchedOperations.Add(copyOperation);
                tasks.Add(_operationService.EnqueueOperationAsync(copyOperation, resource));
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
    }
}