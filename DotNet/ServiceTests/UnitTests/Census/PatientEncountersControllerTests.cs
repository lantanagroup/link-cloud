using LantanaGroup.Link.Census.Controllers;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Census
{
    [Trait("Category", "UnitTests")]
    public class PatientEncountersControllerTests
    {
        private Mock<ILogger<PatientEncountersController>> _loggerMock;
        private Mock<IPatientEncounterManager> _managerMock;
        private Mock<IPatientEncounterQueries> _queriesMock;
        private PatientEncountersController _controller;

        public PatientEncountersControllerTests()
        {
            _loggerMock = new Mock<ILogger<PatientEncountersController>>();
            _managerMock = new Mock<IPatientEncounterManager>();
            _queriesMock = new Mock<IPatientEncounterQueries>();
            _controller = new PatientEncountersController(_loggerMock.Object, _managerMock.Object, _queriesMock.Object);
        }

        [Fact]
        public async Task GetCurrentPatientEncounters_ReturnsBadRequest_WhenFacilityIdMissing()
        {
            // Act
            var result = await _controller.GetCurrentPatientEncounters("", null, null, null, 10, 1, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("facilityId is required.", badRequest.Value);
        }

        [Fact]
        public async Task GetCurrentPatientEncounters_ReturnsEmptyList_WhenNoRecords()
        {
            // Arrange
            var emptyPaged = new PagedConfigModel<PatientEncounterModel>
            {
                Records = new List<PatientEncounterModel>(),
                Metadata = new PaginationMetadata { TotalCount = 0, PageSize = 10, PageNumber = 1, TotalPages = 0 }
            };
            _queriesMock.Setup(q => q.SearchAsync(It.Is<SearchPatientEncounterModel>(m =>
                m.FacilityId == "TestFacility" &&
                m.CorrelationId == null &&
                m.Threshold == null &&
                m.PageSize == 10 &&
                m.PageNumber == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyPaged);

            // Act
            var result = await _controller.GetCurrentPatientEncounters("TestFacility", null, null, null, 10, 1, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Empty(pagedResult.Records);
        }

        [Fact]
        public async Task GetCurrentPatientEncounters_ReturnsOk_WithPatientEncounters()
        {
            // Arrange
            var expectedRecords = new List<PatientEncounterModel>
            {
                new PatientEncounterModel { FacilityId = "TestFacility", CorrelationId = "corr1", AdmitDate = DateTime.UtcNow }
            };
            var expectedPaged = new PagedConfigModel<PatientEncounterModel>
            {
                Records = expectedRecords,
                Metadata = new PaginationMetadata { TotalCount = 1, PageSize = 10, PageNumber = 1, TotalPages = 1 }
            };
            _queriesMock.Setup(q => q.SearchAsync(It.Is<SearchPatientEncounterModel>(m =>
                m.FacilityId == "TestFacility" &&
                m.CorrelationId == null &&
                m.Threshold == null &&
                m.PageSize == 10 &&
                m.PageNumber == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPaged);

            // Act
            var result = await _controller.GetCurrentPatientEncounters("TestFacility", null, null, null, 10, 1, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Single(pagedResult.Records);
            Assert.Equal("corr1", pagedResult.Records.First().CorrelationId);
        }

        [Fact]
        public async Task GetCurrentPatientEncounters_ReturnsProblem_OnException()
        {
            // Arrange
            _queriesMock.Setup(q => q.SearchAsync(It.IsAny<SearchPatientEncounterModel>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetCurrentPatientEncounters("TestFacility", null, null, null, 10, 1, CancellationToken.None);

            // Assert
            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.StatusCode);
            var details = Assert.IsType<ProblemDetails>(problem.Value);
            Assert.Equal("An error occurred while processing your request.", details.Detail);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving patient encounters")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsBadRequest_WhenFacilityIdMissing()
        {
            // Act
            var result = await _controller.GetHistoricalMaterializedView("", null, DateTime.UtcNow, null, null, 10, 1, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("facilityId is required.", badRequest.Value);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsBadRequest_WhenDateThresholdMissing()
        {
            // Act
            var result = await _controller.GetHistoricalMaterializedView("TestFacility", null, null, null, null, 10, 1, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("dateThreshold is required.", badRequest.Value);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsOk_WithPatientEncounters()
        {
            // Arrange
            var dateThreshold = DateTime.UtcNow;
            var expectedRecords = new List<PatientEncounterModel>
            {
                new PatientEncounterModel { FacilityId = "TestFacility", CorrelationId = "corr1", AdmitDate = DateTime.UtcNow.AddDays(-1) }
            };
            var expectedPaged = new PagedConfigModel<PatientEncounterModel>
            {
                Records = expectedRecords,
                Metadata = new PaginationMetadata { TotalCount = 1, PageSize = 10, PageNumber = 1, TotalPages = 1 }
            };
            _queriesMock.Setup(q => q.SearchAsync(It.Is<SearchPatientEncounterModel>(m =>
                m.FacilityId == "TestFacility" &&
                m.CorrelationId == null &&
                m.Threshold == dateThreshold &&
                m.PageSize == 10 &&
                m.PageNumber == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPaged);

            // Act
            var result = await _controller.GetHistoricalMaterializedView("TestFacility", null, dateThreshold, null, null, 10, 1, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Single(pagedResult.Records);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsProblem_OnException()
        {
            // Arrange
            var dateThreshold = DateTime.UtcNow;
            _queriesMock.Setup(q => q.SearchAsync(It.IsAny<SearchPatientEncounterModel>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetHistoricalMaterializedView("TestFacility", null, dateThreshold, null, null, 10, 1, CancellationToken.None);

            // Assert
            var problem = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.StatusCode);
            var details = Assert.IsType<ProblemDetails>(problem.Value);
            Assert.Equal("An error occurred while processing your request.", details.Detail);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving historical materialized view")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RebuildMaterializedView_ReturnsBadRequest_WhenFacilityIdMissing()
        {
            // Act
            var result = await _controller.RebuildMaterializedView("", null, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("facilityId is required.", badRequest.Value);
        }

        [Fact]
        public async Task RebuildMaterializedView_ReturnsAccepted_WhenSuccessful()
        {
            // Arrange
            _queriesMock.Setup(q => q.RebuildPatientEncounterTable(It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RebuildMaterializedView("TestFacility", null, CancellationToken.None);

            // Assert
            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task RebuildMaterializedView_ReturnsProblem_OnException()
        {
            // Arrange
            _queriesMock.Setup(q => q.RebuildPatientEncounterTable(It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.RebuildMaterializedView("TestFacility", null, CancellationToken.None);

            // Assert
            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.StatusCode);
            var details = Assert.IsType<ProblemDetails>(problem.Value);
            Assert.Equal("An error occurred while processing your request.", details.Detail);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error rebuilding materialized view")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetCurrentPatientEncounters_WithCorrelationId_PassesToQueries()
        {
            // Arrange
            var correlationId = "testCorr";
            var expectedPaged = new PagedConfigModel<PatientEncounterModel>
            {
                Records = new List<PatientEncounterModel>(),
                Metadata = new PaginationMetadata { TotalCount = 0, PageSize = 10, PageNumber = 1, TotalPages = 0 }
            };
            _queriesMock.Setup(q => q.SearchAsync(It.Is<SearchPatientEncounterModel>(m =>
                m.FacilityId == "TestFacility" &&
                m.CorrelationId == correlationId &&
                m.Threshold == null), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPaged);

            // Act
            var result = await _controller.GetCurrentPatientEncounters("TestFacility", correlationId, null, null, 10, 1, CancellationToken.None);

            // Assert
            _queriesMock.Verify(q => q.SearchAsync(It.Is<SearchPatientEncounterModel>(m =>
                m.FacilityId == "TestFacility" &&
                m.CorrelationId == correlationId &&
                m.Threshold == null &&
                m.PageSize == 10 &&
                m.PageNumber == 1), It.IsAny<CancellationToken>()), Times.Once);
            var okResult = Assert.IsType<OkObjectResult>(result.Result); // Since empty
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Empty(pagedResult.Records);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_WithSortingAndPaging_PassesParametersCorrectly()
        {
            // Arrange
            var dateThreshold = DateTime.UtcNow;
            var sortBy = "AdmitDate";
            var sortOrder = SortOrder.Descending;
            var pageSize = 20;
            var pageNumber = 2;
            var expectedPaged = new PagedConfigModel<PatientEncounterModel>
            {
                Records = new List<PatientEncounterModel>(),
                Metadata = new PaginationMetadata { TotalCount = 0, PageSize = pageSize, PageNumber = pageNumber, TotalPages = 0 }
            };
            _queriesMock.Setup(q => q.SearchAsync(It.Is<SearchPatientEncounterModel>(m =>
                m.FacilityId == "TestFacility" &&
                m.CorrelationId == null &&
                m.Threshold == dateThreshold &&
                m.SortBy == sortBy &&
                m.SortOrder == sortOrder &&
                m.PageSize == pageSize &&
                m.PageNumber == pageNumber), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPaged);

            // Act
            var result = await _controller.GetHistoricalMaterializedView("TestFacility", null, dateThreshold, sortBy, sortOrder, pageSize, pageNumber, CancellationToken.None);

            // Assert
            _queriesMock.Verify(q => q.SearchAsync(It.Is<SearchPatientEncounterModel>(m =>
                m.FacilityId == "TestFacility" &&
                m.CorrelationId == null &&
                m.Threshold == dateThreshold &&
                m.SortBy == sortBy &&
                m.SortOrder == sortOrder &&
                m.PageSize == pageSize &&
                m.PageNumber == pageNumber), It.IsAny<CancellationToken>()), Times.Once);
            var okResult = Assert.IsType<OkObjectResult>(result.Result); // Since empty
            var pagedResult = Assert.IsType<PagedConfigModel<PatientEncounterModel>>(okResult.Value);
            Assert.Empty(pagedResult.Records);
        }
    }
}