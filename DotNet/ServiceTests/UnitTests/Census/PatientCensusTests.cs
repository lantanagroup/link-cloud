using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.Census.Controllers;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Application.Models.Api;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Census
{
    [Trait("Category", "UnitTests")]
    public class PatientCensusTests
    {
        [Fact]
        public async Task GetCurrentPatientEncounters_ReturnsBadRequest_WhenFacilityIdMissing()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.GetCurrentPatientEncounters("", null, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetCurrentPatientEncounters_ReturnsOk_WithPatientEvents()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            var expected = new List<PatientEncounterModel> { new PatientEncounterModel { FacilityId = "TestFacility" } };
            var encounterModels = new List<PatientEncounterModel> { new PatientEncounterModel { FacilityId = "TestFacility" } };
            manager.Setup(m => m.GetPatientEncounterModels("TestFacility", null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(encounterModels.AsEnumerable());
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.GetCurrentPatientEncounters("TestFacility", null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var actual = Assert.IsAssignableFrom<IEnumerable<PatientEncounterModel>>(okResult.Value);
            Assert.Single(actual);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsBadRequest_WhenFacilityIdMissing()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.GetHistoricalMaterializedView("", null, null, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsBadRequest_WhenDateThresholdMissing()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.GetHistoricalMaterializedView("TestFacility", null, null, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsNotFound_WhenNoResults()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            queries.Setup(q => q.GetViewAsOf("TestFacility", It.IsAny<System.DateTime>(), null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<PatientEncounterModel>());
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.GetHistoricalMaterializedView("TestFacility", null, System.DateTime.UtcNow, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetHistoricalMaterializedView_ReturnsOk_WhenResultsExist()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            var expected = new List<PatientEncounterModel> { new PatientEncounterModel { FacilityId = "TestFacility" } };
            queries.Setup(q => q.GetViewAsOf("TestFacility", It.IsAny<System.DateTime>(), null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.GetHistoricalMaterializedView("TestFacility", null, System.DateTime.UtcNow, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var actual = Assert.IsAssignableFrom<IEnumerable<PatientEncounterModel>>(okResult.Value);
            Assert.Single(actual);
        }

        [Fact]
        public async Task RebuildMaterializedView_ReturnsBadRequest_WhenFacilityIdMissing()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.RebuildMaterializedView("", null, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RebuildMaterializedView_ReturnsAccepted_WhenSuccess()
        {
            var logger = new Mock<ILogger<PatientEncountersController>>();
            var manager = new Mock<IPatientEncounterManager>();
            var queries = new Mock<IPatientEncounterQueries>();
            queries.Setup(q => q.RebuildPatientEncounterTable(It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);
            var controller = new PatientEncountersController(logger.Object, manager.Object, queries.Object);

            var result = await controller.RebuildMaterializedView("TestFacility", null, CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
        }
    }
}
