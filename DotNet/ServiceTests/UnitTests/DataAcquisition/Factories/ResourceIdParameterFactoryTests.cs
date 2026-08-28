using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Factories
{
    [Trait("Category", "UnitTests")]
    public class ResourceIdParameterFactoryTests
    {
        private readonly Mock<IDataAcquisitionLogQueries> _dataAcquisitionLogQueriesMock;
        private readonly IResourceIdParameterFactory _resourceIdParameterFactory;

        public ResourceIdParameterFactoryTests()
        {
            _dataAcquisitionLogQueriesMock = new Mock<IDataAcquisitionLogQueries>();
            var logger = new Mock<ILogger<ResourceIdParameterFactory>>().Object;
            _resourceIdParameterFactory = new ResourceIdParameterFactory(logger);
        }

        [Fact]
        public async Task Build_WhenPagedNotSet_ReturnsJoinedEntries()
        {
            // Arrange
            var parameter = new ResourceIdsParameter
            {
                Name = "TestParam",
                Resource = "Patient",
                Paged = null
            };
            var request = new GetPatientDataRequest
            {
                CorrelationId = "CorrId",
                FacilityId = "FacId"
            };
            var resourceIds = new List<string> { "id1", "id2", "id3" };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(request.CorrelationId, request.FacilityId, It.IsAny<string?>(), parameter.Resource, It.IsAny<CancellationToken>()))
                .ReturnsAsync(resourceIds);

            // Act
            var result = await _resourceIdParameterFactory.Build(parameter, request, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(parameter.Name, result.key);
            Assert.Equal("id1,id2,id3", result.value);
            Assert.False(result.paged);
            Assert.Null(result.values);
        }

        [Fact]
        public async Task Build_WhenPagedSetAndResultsLessThanPageSize_ReturnsJoinedEntries()
        {
            // Arrange
            var parameter = new ResourceIdsParameter
            {
                Name = "TestParam",
                Resource = "Patient",
                Paged = "5"
            };
            var request = new GetPatientDataRequest
            {
                CorrelationId = "CorrId",
                FacilityId = "FacId"
            };
            var resourceIds = new List<string> { "id1", "id2", "id3" };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(request.CorrelationId, request.FacilityId, It.IsAny<string?>(), parameter.Resource, It.IsAny<CancellationToken>()))
                .ReturnsAsync(resourceIds);

            // Act
            var result = await _resourceIdParameterFactory.Build(parameter, request, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("id1,id2,id3", result.value);
            Assert.False(result.paged);
            Assert.Null(result.values);
        }

        [Fact]
        public async Task Build_WhenPagedSetAndResultsMultiplePages_ReturnsPagedEntries()
        {
            // Arrange
            var parameter = new ResourceIdsParameter
            {
                Name = "TestParam",
                Resource = "Patient",
                Paged = "2"
            };
            var request = new GetPatientDataRequest
            {
                CorrelationId = "CorrId",
                FacilityId = "FacId"
            };
            var resourceIds = new List<string> { "id1", "id2", "id3", "id4", "id5" };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(request.CorrelationId, request.FacilityId, It.IsAny<string?>(), parameter.Resource, It.IsAny<CancellationToken>()))
                .ReturnsAsync(resourceIds);

            // Act
            var result = await _resourceIdParameterFactory.Build(parameter, request, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.paged);
            Assert.Null(result.value);
            Assert.NotNull(result.values);
            Assert.Equal(3, result.values.Count);
            Assert.Equal(new[] { "id1", "id2" }, result.values[0]);
            Assert.Equal(new[] { "id3", "id4" }, result.values[1]);
            Assert.Equal(new[] { "id5" }, result.values[2]);
        }

        [Fact]
        public async Task Build_WhenNoResourceIdsFound_ReturnsNull()
        {
            // Arrange
            var parameter = new ResourceIdsParameter
            {
                Name = "TestParam",
                Resource = "Patient"
            };
            var request = new GetPatientDataRequest
            {
                CorrelationId = "CorrId",
                FacilityId = "FacId"
            };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(request.CorrelationId, request.FacilityId, It.IsAny<string?>(), parameter.Resource, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            // Act
            var result = await _resourceIdParameterFactory.Build(parameter, request, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Build_WhenScheduledReportPresent_ForwardsReportTrackingId()
        {
            var parameter = new ResourceIdsParameter
            {
                Name = "TestParam",
                Resource = "Encounter"
            };
            var reportTrackingId = Guid.NewGuid().ToString();
            var request = new GetPatientDataRequest
            {
                CorrelationId = "CorrId",
                FacilityId = "FacId",
                ConsumeResult = new Confluent.Kafka.ConsumeResult<string, LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka.DataAcquisitionRequested>
                {
                    Message = new Confluent.Kafka.Message<string, LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka.DataAcquisitionRequested>
                    {
                        Value = new LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka.DataAcquisitionRequested
                        {
                            PatientId = "Patient/1",
                            ScheduledReports =
                            [
                                new LantanaGroup.Link.Shared.Application.Models.ScheduledReport
                                {
                                    ReportTrackingId = reportTrackingId
                                }
                            ]
                        }
                    }
                }
            };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(
                    request.CorrelationId,
                    request.FacilityId,
                    reportTrackingId,
                    parameter.Resource,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(["enc-1"]);

            var result = await _resourceIdParameterFactory.Build(parameter, request, _dataAcquisitionLogQueriesMock.Object);

            Assert.NotNull(result);
            Assert.Equal("enc-1", result.value);
            _dataAcquisitionLogQueriesMock.Verify(
                q => q.GetResourceIdsForReportPatient(
                    request.CorrelationId,
                    request.FacilityId,
                    reportTrackingId,
                    parameter.Resource,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
