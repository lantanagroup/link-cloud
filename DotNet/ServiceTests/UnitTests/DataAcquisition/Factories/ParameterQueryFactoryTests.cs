using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Factories
{
    [Trait("Category", "UnitTests")]
    public class ParameterQueryFactoryTests
    {
        private readonly Mock<IDataAcquisitionLogQueries> _dataAcquisitionLogQueriesMock;
        private readonly IParameterQueryFactory _parameterQueryFactory;

        public ParameterQueryFactoryTests()
        {
            _dataAcquisitionLogQueriesMock = new Mock<IDataAcquisitionLogQueries>();

            var logger = new Mock<ILogger<ParameterQueryFactory>>().Object;
            var literalLogger = new Mock<ILogger<LiteralParameterFactory>>().Object;
            var variableLogger = new Mock<ILogger<VariableParameterFactory>>().Object;
            var resourceIdLogger = new Mock<ILogger<ResourceIdParameterFactory>>().Object;

            var literalFactory = new LiteralParameterFactory(literalLogger);
            var variableFactory = new VariableParameterFactory(variableLogger);
            var resourceIdFactory = new ResourceIdParameterFactory(resourceIdLogger);

            _parameterQueryFactory = new ParameterQueryFactory(logger, literalFactory, variableFactory, resourceIdFactory);
        }

        [Fact]
        public async Task Build_WhenNoParametersArePaged_ReturnsSingularResult()
        {
            // Arrange
            var config = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "lit1", Literal = "val1" },
                    new LiteralParameter { Name = "lit2", Literal = "val2" }
                }
            };
            var request = new GetPatientDataRequest { CorrelationId = "corr1", FacilityId = "fac1" };

            // Act
            var result = await _parameterQueryFactory.Build(config, request, null, null, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            var singularResult = Assert.IsType<SingularParameterQueryFactoryResult>(result);
            Assert.Equal(2, singularResult.SearchParams.Count);
            Assert.Contains(singularResult.SearchParams, kvp => kvp.Key == "lit1" && kvp.Value == "val1");
            Assert.Contains(singularResult.SearchParams, kvp => kvp.Key == "lit2" && kvp.Value == "val2");
        }

        [Fact]
        public async Task Build_WhenOneParameterIsPaged_ReturnsPagedResult()
        {
            // Arrange
            var config = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "lit1", Literal = "val1" },
                    new ResourceIdsParameter { Name = "_id", Resource = "Patient", Paged = "2" }
                }
            };
            var request = new GetPatientDataRequest { CorrelationId = "corr1", FacilityId = "fac1" };
            var resourceIds = new List<string> { "id1", "id2", "id3" };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(request.CorrelationId, request.FacilityId, It.IsAny<string?>(), "Patient", It.IsAny<CancellationToken>()))
                .ReturnsAsync(resourceIds);

            // Act
            var result = await _parameterQueryFactory.Build(config, request, null, null, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            var pagedResult = Assert.IsType<PagedParameterQueryFactoryResult>(result);
            Assert.Equal(2, pagedResult.SearchParamsList.Count);

            // Page 1: lit1=val1, _id=id1,id2
            Assert.Contains(pagedResult.SearchParamsList[0], kvp => kvp.Key == "lit1" && kvp.Value == "val1");
            Assert.Contains(pagedResult.SearchParamsList[0], kvp => kvp.Key == "_id" && kvp.Value == "id1,id2");

            // Page 2: lit1=val1, _id=id3
            Assert.Contains(pagedResult.SearchParamsList[1], kvp => kvp.Key == "lit1" && kvp.Value == "val1");
            Assert.Contains(pagedResult.SearchParamsList[1], kvp => kvp.Key == "_id" && kvp.Value == "id3");
        }

        [Fact]
        public async Task Build_WhenMultipleParametersArePaged_ReturnsNull()
        {
            // Arrange
            var config = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new ResourceIdsParameter { Name = "_id", Resource = "Patient", Paged = "2" },
                    new ResourceIdsParameter { Name = "other", Resource = "Other", Paged = "1" }
                }
            };
            var request = new GetPatientDataRequest { CorrelationId = "corr1", FacilityId = "fac1" };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "id1", "id2", "id3" });

            // Act
            var result = await _parameterQueryFactory.Build(config, request, null, null, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Build_WhenParameterFactoryReturnsNull_SkipsIt()
        {
            // Arrange
            var config = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "", Literal = "" }, // Invalid literal should return null from factory
                    new LiteralParameter { Name = "lit2", Literal = "val2" }
                }
            };
            var request = new GetPatientDataRequest { CorrelationId = "corr1", FacilityId = "fac1" };

            // Act
            var result = await _parameterQueryFactory.Build(config, request, null, null, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            var singularResult = Assert.IsType<SingularParameterQueryFactoryResult>(result);
            Assert.Single(singularResult.SearchParams);
            Assert.Equal("lit2", singularResult.SearchParams[0].Key);
        }

        [Theory]
        [InlineData(OperationType.Search, OperationType.Search)]
        [InlineData(OperationType.SearchPost, OperationType.SearchPost)]
        [InlineData(OperationType.Read, OperationType.Search)] // Read is invalid for parameter queries; must fall back to Search
        public async Task Build_SingularResult_UsesExpectedOperationType(OperationType configured, OperationType expected)
        {
            // Arrange
            var config = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                OperationType = configured,
                Parameters = new List<IParameter>
                {
                    new LiteralParameter { Name = "lit1", Literal = "val1" }
                }
            };
            var request = new GetPatientDataRequest { CorrelationId = "corr1", FacilityId = "fac1" };

            // Act
            var result = await _parameterQueryFactory.Build(config, request, null, null, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            var singularResult = Assert.IsType<SingularParameterQueryFactoryResult>(result);
            Assert.Equal(expected, singularResult.opType);
        }

        [Theory]
        [InlineData(OperationType.Search, OperationType.Search)]
        [InlineData(OperationType.SearchPost, OperationType.SearchPost)]
        [InlineData(OperationType.Read, OperationType.Search)] // Read is invalid for parameter queries; must fall back to Search
        public async Task Build_PagedResult_UsesExpectedOperationType(OperationType configured, OperationType expected)
        {
            // Arrange
            var config = new ParameterQueryConfig
            {
                ResourceType = "Patient",
                OperationType = configured,
                Parameters = new List<IParameter>
                {
                    new ResourceIdsParameter { Name = "_id", Resource = "Patient", Paged = "2" }
                }
            };
            var request = new GetPatientDataRequest { CorrelationId = "corr1", FacilityId = "fac1" };

            _dataAcquisitionLogQueriesMock
                .Setup(q => q.GetResourceIdsForReportPatient(request.CorrelationId, request.FacilityId, It.IsAny<string?>(), "Patient", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "id1", "id2", "id3" });

            // Act
            var result = await _parameterQueryFactory.Build(config, request, null, null, _dataAcquisitionLogQueriesMock.Object);

            // Assert
            var pagedResult = Assert.IsType<PagedParameterQueryFactoryResult>(result);
            Assert.Equal(expected, pagedResult.opType);
        }
    }
}
