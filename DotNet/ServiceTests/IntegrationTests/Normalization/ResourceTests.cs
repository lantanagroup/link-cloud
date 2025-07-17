using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization
{
    [Collection("NormalizationIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ResourceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly NormalizationIntegrationTestFixture _fixture;

        public ResourceTests(NormalizationIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task Resource_Data_Create_Get_Delete()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();
            var resourceQueries = scope.ServiceProvider.GetRequiredService<IResourceQueries>();

            var resourceName = Guid.NewGuid().ToString();
            var resource = await resourceManager.CreateResource(resourceName, true);

            Assert.NotNull(resource);
            Assert.Equal(resourceName, resource.ResourceName);

            var resourceGet = await resourceQueries.Get(resource.ResourceTypeId);

            Assert.NotNull(resourceGet);
            Assert.Equal(resourceName, resourceGet.ResourceName);

            await resourceManager.DeleteResource(resourceName);

            resourceGet = await resourceQueries.Get(resource.ResourceTypeId);

            Assert.Null(resourceGet);
        }

        [Fact]
        public async Task Resource_Get_All()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceQueries = scope.ServiceProvider.GetRequiredService<IResourceQueries>();

            var resources = await resourceQueries.GetAll();

            Assert.NotNull(resources);
            Assert.NotEmpty(resources);

            List<string> resourceEnumList = new List<string>(Enum.GetNames(typeof(ResourceType)));

            Assert.True(resources.Count >= resourceEnumList.Count);
        }

        [Fact]
        public async Task InitializeResources_AlreadyInitialized_ReturnsEmptyList()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IResourceManager>();

            var result = await manager.InitializeResources();

            Assert.Empty(result);
        }

        [Fact]
        public async Task InitializeResources_AfterDeletingOne_CreatesMissingResource()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();
            var resourceQueries = scope.ServiceProvider.GetRequiredService<IResourceQueries>();

            // Delete an existing resource
            const string resourceName = "Patient";
            await resourceManager.DeleteResource(resourceName);

            // Initialize should recreate it
            var result = await resourceManager.InitializeResources();

            Assert.Contains(result, r => r.ResourceName == resourceName);
            var recreated = await resourceQueries.Get(resourceName);
            Assert.NotNull(recreated);
        }

        [Fact]
        public async Task CreateResource_ValidNewNameWithBypass_CreatesResource()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();
            var resourceQueries = scope.ServiceProvider.GetRequiredService<IResourceQueries>();

            string uniqueName = Guid.NewGuid().ToString();
            var result = await resourceManager.CreateResource(uniqueName, bypassTypeCheck: true);

            Assert.NotNull(result);
            Assert.Equal(uniqueName, result.ResourceName);

            var fetched = await resourceQueries.Get(uniqueName);
            Assert.NotNull(fetched);
        }

        [Fact]
        public async Task CreateResource_ExistingName_ReturnsNull()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();

            const string existingName = "Patient";
            var result = await resourceManager.CreateResource(existingName);

            Assert.Null(result);
        }

        [Fact]
        public async Task CreateResource_InvalidNameWithoutBypass_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();

            string invalidName = Guid.NewGuid().ToString();
            await Assert.ThrowsAsync<InvalidOperationException>(() => resourceManager.CreateResource(invalidName));
        }

        [Fact]
        public async Task CreateResource_NullName_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => resourceManager.CreateResource(null));
        }

        [Fact]
        public async Task DeleteResource_ExistingName_DeletesSuccessfully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();
            var resourceQueries = scope.ServiceProvider.GetRequiredService<IResourceQueries>();

            string uniqueName = Guid.NewGuid().ToString();
            await resourceManager.CreateResource(uniqueName, bypassTypeCheck: true);

            await resourceManager.DeleteResource(uniqueName);

            var fetched = await resourceQueries.Get(uniqueName);
            Assert.Null(fetched);
        }

        [Fact]
        public async Task DeleteResource_NonExistingName_ThrowsException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();

            string nonExisting = Guid.NewGuid().ToString();
            await Assert.ThrowsAsync<InvalidOperationException>(() => resourceManager.DeleteResource(nonExisting));
        }
    }
}