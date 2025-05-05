using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using NormalizationTests;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace NormalizationOperationTests
{
    public class NormalizationOperationTests : IClassFixture<IntegrationTestFixture>
    {
        private readonly IntegrationTestFixture _fixture;

        public NormalizationOperationTests(IntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }


        [Fact]
        public void Unit_Location_Identifier_To_Type()
        {
            var parser = new FhirJsonParser();
            string location_text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Location.txt"));
            var location = parser.Parse<Location>(location_text);

            if (location == null)
            {
                Assert.Fail("No location resource found");
            }

            CopyPropertyOperation copyOperation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            copyOperation.Execute(location);

            Assert.Equal(location.Identifier[0].Value, location.Type[0].Coding[0].Code);
        }

        [Fact]
        public async Task Integration_CopyPropertyOperation_Location_Identifier_To_Type()
        {
            var database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();

            await database.ResourceTypes.AddAsync(new LantanaGroup.Link.Normalization.Domain.Entities.ResourceType()
            {
                Name = "Location",
            });

            await database.ResourceTypes.SaveChangesAsync();

            var manaager = _fixture.ServiceProvider.GetRequiredService<IOperationManager>();

            var operation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            var result = await manaager.CreateOperation(new CreateOperationModel()
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
            string location_text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Location.txt"));
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

            copyOperation.Execute(location);

            Assert.Equal(location.Identifier[0].Value, location.Type[0].Coding[0].Code);
        }

    }
}