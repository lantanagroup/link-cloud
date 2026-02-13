using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers
{
    [Trait("Category", "UnitTests")]
    public class QueryListControllerTests
    {
        private AutoMocker _mocker;
        private QueryListController _controller;
        private const string FacilityId = "test-facility";

        public QueryListControllerTests()
        {
            _mocker = new AutoMocker();
            
            // Mock IOptions<ApiSettings>
            var apiSettings = new ApiSettings();
            _mocker.Use(Options.Create(apiSettings));

            _controller = _mocker.CreateInstance<QueryListController>();
        }

        [Fact]
        public async Task GetFhirConfiguration_ReturnsOk_WhenFound()
        {
            // Arrange
            var expectedConfig = new FhirListConfigurationModel
            {
                FacilityId = FacilityId,
                FhirBaseServerUrl = "http://test-server.com"
            };

            _mocker.GetMock<IFhirQueryListConfigurationQueries>()
                .Setup(x => x.GetByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedConfig);

            // Act
            var result = await _controller.GetFhirConfiguration(FacilityId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedConfig = Assert.IsType<FhirListConfigurationModel>(okResult.Value);
            Assert.Equal(expectedConfig.FacilityId, returnedConfig.FacilityId);
            Assert.Equal(expectedConfig.FhirBaseServerUrl, returnedConfig.FhirBaseServerUrl);

            _mocker.GetMock<IFhirQueryListConfigurationQueries>()
                .Verify(x => x.GetByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetFhirConfiguration_ReturnsNotFound_WhenNotFound()
        {
            // Arrange
            _mocker.GetMock<IFhirQueryListConfigurationQueries>()
                .Setup(x => x.GetByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FhirListConfigurationModel?)null);

            // Act
            var result = await _controller.GetFhirConfiguration(FacilityId, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetFhirConfiguration_ReturnsBadRequest_WhenFacilityIdIsEmpty()
        {
            // Act
            var result = await _controller.GetFhirConfiguration("", CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("A facility id is required.", badRequestResult.Value);
        }

        [Fact]
        public async Task GetFhirConfiguration_LogsAndThrows_WhenExceptionOccurs()
        {
            // Arrange
            var exception = new Exception("Database error");
            _mocker.GetMock<IFhirQueryListConfigurationQueries>()
                .Setup(x => x.GetByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act & Assert
            var thrownException = await Assert.ThrowsAsync<Exception>(() => _controller.GetFhirConfiguration(FacilityId, CancellationToken.None));
            Assert.Same(exception, thrownException);

            _mocker.GetMock<ILogger<QueryListController>>().Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An exception occurred while attempting to get a fhir query configuration")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
