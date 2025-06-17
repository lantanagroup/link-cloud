using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.IntegrationTests.Normalization
{
    [Collection("NormalizationIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class NormalizationResourceManagerQueryTests
    {
        private readonly ITestOutputHelper _output;
        private readonly NormalizationIntegrationTestFixture _fixture;
        private readonly IDatabase _database;
        private readonly IResourceManager _resourceManager;
        private readonly IResourceQueries _resourceQueries;

        public NormalizationResourceManagerQueryTests(NormalizationIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            _resourceManager = _fixture.ServiceProvider.GetRequiredService<IResourceManager>();
            _resourceQueries = _fixture.ServiceProvider.GetRequiredService<IResourceQueries>();
        }

        [Fact]
        public async Task Resource_Data_Create_Get_Delete()
        {
            var resourceName = "IntegrationTestResourceType";
            var resource = await _resourceManager.CreateResource(resourceName, true);

            Assert.NotNull(resource);
            Assert.Equal(resourceName, resource.ResourceName);

            var resourceGet = await _resourceQueries.Get(resource.ResourceTypeId);

            Assert.NotNull(resourceGet);
            Assert.Equal(resourceName, resourceGet.ResourceName);

            await _resourceManager.DeleteResource(resourceName);

            resourceGet = await _resourceQueries.Get(resource.ResourceTypeId);

            Assert.Null(resourceGet);
        }

        [Fact]
        public async Task Resource_Get_All()
        {
            var resources = await _resourceQueries.GetAll();

            Assert.NotNull(resources);
            Assert.NotEmpty(resources);

            List<string> resourceEnumList = new List<string>(Enum.GetNames(typeof(ResourceType)));

            Assert.True(resources.Count >= resourceEnumList.Count);
        }
    }
}
