using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using NormalizationTests;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace NormalizationOperationTests
{
    [Collection("DatabaseCollection")]
    public class NormalizationOperationTests : IClassFixture<IntegrationTestFixture>
    {
        private readonly ITestOutputHelper _output;
        private readonly IntegrationTestFixture _fixture;
        private readonly IDatabase _database;
        private readonly IOperationManager _operationManager;

        public NormalizationOperationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            _operationManager = _fixture.ServiceProvider.GetRequiredService<IOperationManager>();            
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

            location = (Location)copyOperation.Execute(location);

            _output.WriteLine("Original: ");
            _output.WriteLine(location_text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(location));

            Assert.Equal(location.Identifier[0].Value, location.Type[0].Coding[0].Code);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

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

            location = (Location)copyOperation.Execute(location);

            _output.WriteLine("Original: ");
            _output.WriteLine(location_text);

            _output.WriteLine("Modified: ");            
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(location));

            Assert.Equal(location.Identifier[0].Value, location.Type[0].Coding[0].Code);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string locationPath = Path.Combine(assemblyLocation, "Resources", "LocationWithCodeSection.txt");
            string location_text = File.ReadAllText(locationPath);
            var location = parser.Parse<Location>(location_text);

            PostOperationModel model = new PostOperationModel()
            {
                ResourceTypes = ["Location"],
                Operation = operation,
                Description = "Description For Daniel Son, The God King",
                FacilityId = null,
            };

            var options = new JsonSerializerOptions
            {
                Converters = { new OperationConverter() }
            };

            _output.WriteLine(JsonSerializer.Serialize(model, options));

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            location = (Location)copyOperation.Execute(location);

            _output.WriteLine("Original: ");
            _output.WriteLine(location_text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(location));

            Assert.Equal(location.Identifier[0].Value, location.Type[0].Coding[0].Code);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

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

            resource = (Patient)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            Assert.Equal(resource.Identifier[0].Value, resource.Name[0].Family);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Observation_Identifier_To_ValueQuanitty_Update_TargetElement()
        {
            var operation = new CopyPropertyOperation("Copy Observation Identifier to ValueQuantity", "valueQuantity.value", "component.valueQuantity.value");

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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

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

            resource = (Observation)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the target values match the source
            Assert.All(resource.Component, component =>
            {
                Assert.IsType<Quantity>(component.Value);
                var quantity = (Quantity)component.Value;
                Assert.Equal(120m, quantity.Value);
            });
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Patient_NameToExtension_CreateTarget()
        {
            var operation = new CopyPropertyOperation(
                "Copy Patient Name to Extension",
                "name[0].given[0]",
                "extension[0].valueString"
            );

            var result = await _fixture.ServiceProvider.GetRequiredService<IOperationManager>().CreateOperation(new CreateOperationModel
            {
                OperationJson = JsonSerializer.Serialize<CopyPropertyOperation>(operation),
                OperationType = OperationType.CopyProperty.ToString(),
                FacilityId = null,
                Description = "Integration Test Copy Patient Name to Extension",
                IsDisabled = false,
                ResourceTypes = ["Patient"]
            });

            Assert.NotNull(result);
            Assert.NotEqual(default(Guid), result.Id);

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

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

            resource = (Patient)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the extension was created and set correctly
            Assert.NotEmpty(resource.Extension);
            var extension = resource.Extension.FirstOrDefault(e => e.Url == "http://example.org/fhir/extension/Patient-GivenName");
            Assert.NotNull(extension);
            Assert.IsType<FhirString>(extension.Value);
            Assert.Equal("John", ((FhirString)extension.Value).Value);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_MedicationRequest_DosageToNote_CreateTarget()
        {
            var operation = new CopyPropertyOperation(
                "Copy MedicationRequest Dosage to Note",
                "dosageInstruction[0].doseAndRate[0].dose.value",
                "note[0].text"
            );

            var result = await _fixture.ServiceProvider.GetRequiredService<IOperationManager>().CreateOperation(new CreateOperationModel
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

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

            resource = (MedicationRequest)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the note was created and set correctly
            Assert.NotEmpty(resource.Note);
            Assert.Equal(1, resource.Note.Count);
            var note = resource.Note[0];
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

            var result = await _fixture.ServiceProvider.GetRequiredService<IOperationManager>().CreateOperation(new CreateOperationModel
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

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

            resource = (Condition)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the code.text was updated correctly
            Assert.NotNull(resource.Code);
            Assert.NotNull(resource.Code.Text);
            Assert.Equal("2023-05-01T10:00:00Z", resource.Code.Text);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "EncounterCopyTest.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Encounter>(text);

            // Validate resource structure
            Assert.NotNull(resource.Period);
            Assert.NotNull(resource.Period.StartElement);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            resource = (Encounter)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the target value matches the source
            Assert.NotEmpty(resource.ReasonCode);
            Assert.NotNull(resource.ReasonCode[0].Text);
            Assert.Equal(resource.Period.StartElement.ToString(), resource.ReasonCode[0].Text);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "PatientBirthDateTest.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<Patient>(text);

            // Validate resource structure
            Assert.NotNull(resource.BirthDateElement);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            resource = (Patient)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the target value matches the source
            Assert.NotEmpty(resource.Name);
            Assert.NotNull(resource.Name[0].Text);
            Assert.Equal(resource.BirthDateElement.ToString(), resource.Name[0].Text);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "MedicationRequestAuthoredOnToNote.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<MedicationRequest>(text);

            // Validate resource structure
            Assert.NotNull(resource.AuthoredOnElement);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            resource = (MedicationRequest)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the target value matches the source
            Assert.NotEmpty(resource.Note);
            Assert.NotNull(resource.Note[0].Text);
            Assert.Equal(resource.AuthoredOnElement.ToString(), resource.Note[0].Text);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "AllergyIntolerance.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<AllergyIntolerance>(text);

            // Validate resource structure
            Assert.NotNull(resource.ClinicalStatus);
            Assert.NotEmpty(resource.ClinicalStatus.Coding);
            Assert.NotNull(resource.ClinicalStatus.Coding[0].Code);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            resource = (AllergyIntolerance)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the target value matches the source
            Assert.NotEmpty(resource.Reaction);
            Assert.NotNull(resource.Reaction[0].Description);
            Assert.Equal(resource.ClinicalStatus.Coding[0].Code, resource.Reaction[0].Description);
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

            var queries = _fixture.ServiceProvider.GetRequiredService<IOperationQueries>();
            var fetched = await queries.Get(result.Id);

            Assert.NotNull(fetched);
            Assert.True(fetched.Id != default);

            var parser = new FhirJsonParser();
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string resourcePath = Path.Combine(assemblyLocation, "Resources", "DiagnosticReport.txt");
            string text = File.ReadAllText(resourcePath);
            var resource = parser.Parse<DiagnosticReport>(text);

            // Validate resource structure
            Assert.NotNull(resource.Effective);
            Assert.True(resource.Effective is FhirDateTime, "The DiagnosticReport resource effective field must be a FhirDateTime.");
            var effectiveDateTime = resource.Effective as FhirDateTime;
            Assert.NotNull(effectiveDateTime.Value);

            Assert.NotNull(fetched.OperationJson);
            var copyOperation = JsonSerializer.Deserialize<CopyPropertyOperation>(fetched.OperationJson);

            Assert.NotNull(copyOperation);
            Assert.NotNull(copyOperation.SourceFhirPath);
            Assert.NotNull(copyOperation.TargetFhirPath);

            resource = (DiagnosticReport)copyOperation.Execute(resource);

            _output.WriteLine("Original: ");
            _output.WriteLine(text);

            _output.WriteLine("Modified: ");
            FhirJsonSerializer serializer = new FhirJsonSerializer();
            _output.WriteLine(await serializer.SerializeToStringAsync(resource));

            // Assert that the target value matches the source
            Assert.NotNull(resource.Code);
            Assert.NotNull(resource.Code.Text);
            Assert.Equal(((FhirDateTime)resource.Effective).ToString(), resource.Code.Text);
        }
    }
}