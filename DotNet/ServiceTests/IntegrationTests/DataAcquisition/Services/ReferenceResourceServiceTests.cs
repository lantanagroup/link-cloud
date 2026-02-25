using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using IntegrationTests.DataAcquisition;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using RequestStatusEnum = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace IntegrationTests.DataAcquisition.Services
{
    [Collection("DataAcquisitionIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ReferenceResourceServiceTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
    {
        private readonly DataAcquisitionIntegrationTestFixture _fixture;

        public ReferenceResourceServiceTests(DataAcquisitionIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private ReferenceResourceService CreateService(IServiceScope scope, IFhirQueryManager fqManager)
        {
            var logger = new Mock<ILogger<ReferenceResourceService>>().Object;
            var refMgr = new Mock<IReferenceResourcesManager>().Object;
            var refQueries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
            var kafkaProducer = _fixture.ResourceAcquiredProducerMock.Object;

            var metrics = new Mock<IDataAcquisitionServiceMetrics>();
            metrics.Setup(m => m.MeasureDataRequestDuration(It.IsAny<List<KeyValuePair<string, object?>>>()));
            metrics.Setup(m => m.IncrementResourceAcquiredCounter(It.IsAny<List<KeyValuePair<string, object?>>>()));

            var daLogMgr = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var daLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            return new ReferenceResourceService(
                logger,
                refMgr,
                refQueries,
                kafkaProducer,
                metrics.Object,
                daLogMgr,
                daLogQueries,
                fqManager);
        }

        // Placeholder for future ReferenceResourceService tests.
    }
}
