using LantanaGroup.Link.Normalization.Application.Models.Operations;
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
    }
}