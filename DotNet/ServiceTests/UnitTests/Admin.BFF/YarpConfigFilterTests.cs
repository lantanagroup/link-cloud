using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Filters;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Yarp.ReverseProxy.Configuration;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Admin.BFF
{
    [Trait("Category", "UnitTests")]
    public class YarpConfigFilterTests
    {
        private readonly Mock<ILogger<YarpConfigFilter>> _mockLogger;
        private readonly Mock<IOptions<ServiceRegistry>> _mockServiceRegistry;
        private readonly ServiceRegistry _serviceRegistry;

        public YarpConfigFilterTests()
        {
            _mockLogger = new Mock<ILogger<YarpConfigFilter>>();
            _mockServiceRegistry = new Mock<IOptions<ServiceRegistry>>();

            _serviceRegistry = new ServiceRegistry()
            {
                AccountServiceUrl = "http://account-service",
                AuditServiceUrl = "http://audit-service",
                CensusServiceUrl = "http://census-service",
                DataAcquisitionServiceUrl = "http://data-acquisition-service",
                MeasureServiceUrl = "http://measure-service",
                NormalizationServiceUrl = "http://normalization-service",
                NotificationServiceUrl = "http://notification-service",
                QueryDispatchServiceUrl = "http://query-dispatch-service",
                ReportServiceUrl = "http://report-service",
                SubmissionServiceUrl = "http://submission-service",
                ValidationServiceUrl = "http://validation-service",
                TenantService = new TenantServiceRegistration()
                {
                    TenantServiceUrl = "http://tenant-service"
                }
            };

            _mockServiceRegistry.Setup(p => p.Value).Returns(_serviceRegistry);
        }

        [Fact]
        public async Task ConfigureClusterAsync_ValidationService_ReturnsCorrectEndpoint()
        {
            // Arrange
            var filter = new YarpConfigFilter(_mockLogger.Object, _mockServiceRegistry.Object);

            var destinations = new Dictionary<string, DestinationConfig>
            {
                { "destination1", new DestinationConfig { Address = "http://placeholder" } }
            };

            var origCluster = new ClusterConfig
            {
                ClusterId = "ValidationService",
                Destinations = destinations
            };

            // Act
            var result = await filter.ConfigureClusterAsync(origCluster, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Destinations);
            Assert.True(result.Destinations.ContainsKey("destination1"));
            Assert.Equal("http://validation-service", result.Destinations["destination1"].Address);
        }

        [Fact]
        public async Task ConfigureClusterAsync_ValidationServiceUrlEmpty_ReturnsOriginalCluster()
        {
            // Arrange
            var emptyServiceRegistry = new ServiceRegistry()
            {
                ValidationServiceUrl = string.Empty,
                TenantService = new TenantServiceRegistration()
            };
            _mockServiceRegistry.Setup(p => p.Value).Returns(emptyServiceRegistry);

            var filter = new YarpConfigFilter(_mockLogger.Object, _mockServiceRegistry.Object);

            var destinations = new Dictionary<string, DestinationConfig>
            {
                { "destination1", new DestinationConfig { Address = "http://placeholder" } }
            };

            var origCluster = new ClusterConfig
            {
                ClusterId = "ValidationService",
                Destinations = destinations
            };

            // Act
            var result = await filter.ConfigureClusterAsync(origCluster, CancellationToken.None);

            // Assert
            Assert.Equal(origCluster, result);
            Assert.Equal("http://placeholder", result.Destinations["destination1"].Address);
        }

        [Fact]
        public async Task ConfigureClusterAsync_AllServices_ReturnCorrectEndpoints()
        {
            // Arrange
            var filter = new YarpConfigFilter(_mockLogger.Object, _mockServiceRegistry.Object);

            var testCases = new Dictionary<string, string>
            {
                { "AccountService", "http://account-service" },
                { "AuditService", "http://audit-service" },
                { "CensusService", "http://census-service" },
                { "DataAcquisitionService", "http://data-acquisition-service" },
                { "MeasureEvaluationService", "http://measure-service" },
                { "NormalizationService", "http://normalization-service" },
                { "NotificationService", "http://notification-service" },
                { "QueryDispatchService", "http://query-dispatch-service" },
                { "ReportService", "http://report-service" },
                { "SubmissionService", "http://submission-service" },
                { "TenantService", "http://tenant-service" },
                { "ValidationService", "http://validation-service" }
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                var destinations = new Dictionary<string, DestinationConfig>
                {
                    { "destination1", new DestinationConfig { Address = "http://placeholder" } }
                };

                var origCluster = new ClusterConfig
                {
                    ClusterId = testCase.Key,
                    Destinations = destinations
                };

                // Act
                var result = await filter.ConfigureClusterAsync(origCluster, CancellationToken.None);

                // Assert
                Assert.NotNull(result.Destinations);
                Assert.True(result.Destinations.ContainsKey("destination1"));
                Assert.Equal(testCase.Value, result.Destinations["destination1"].Address);
            }
        }

        [Fact]
        public async Task ConfigureClusterAsync_UnknownClusterId_ReturnsOriginalCluster()
        {
            // Arrange
            var filter = new YarpConfigFilter(_mockLogger.Object, _mockServiceRegistry.Object);

            var destinations = new Dictionary<string, DestinationConfig>
            {
                { "destination1", new DestinationConfig { Address = "http://placeholder" } }
            };

            var origCluster = new ClusterConfig
            {
                ClusterId = "UnknownService",
                Destinations = destinations
            };

            // Act
            var result = await filter.ConfigureClusterAsync(origCluster, CancellationToken.None);

            // Assert
            Assert.Equal(origCluster, result);
            Assert.Equal("http://placeholder", result.Destinations["destination1"].Address);
        }
    }
}