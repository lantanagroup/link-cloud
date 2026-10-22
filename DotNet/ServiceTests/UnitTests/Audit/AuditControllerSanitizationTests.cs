using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Presentation.Controllers;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Audit
{
    /// <summary>
    /// The identifier filters are matched exactly, so anything the controller strips from them
    /// turns a real match into an empty result rather than an error. These tests pin the
    /// identifier shapes the filters have to survive.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class AuditControllerSanitizationTests
    {
        private static readonly Meter Meter = new("UnitTests.Audit");

        /// <summary>
        /// Builds a controller whose search repository records the filters it was handed.
        /// </summary>
        private static AuditController BuildController(List<string?> captured)
        {
            var searchRepository = new Mock<ISearchRepository>();
            searchRepository
                .Setup(x => x.SearchAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<SortOrder?>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback((string? searchText, string? facility, string? correlation, string? service,
                    string? action, string? user, string? patient, string? resource, string? resourceId,
                    string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken token) =>
                {
                    captured.Add(patient);
                    captured.Add(resource);
                    captured.Add(resourceId);
                })
                .ReturnsAsync((Array.Empty<AuditLog>(), new PaginationMetadata(10, 1, 0)));

            var metrics = new Mock<IAuditServiceMetrics>();
            metrics.Setup(x => x.MeasureAuditSearchDuration(It.IsAny<List<KeyValuePair<string, object?>>>()))
                .Returns(new TrackedRequestDuration(Meter.CreateHistogram<double>("search"), TimeProvider.System, []));

            return new AuditController(new Mock<ILogger<AuditController>>().Object, metrics.Object,
                searchRepository.Object, new Mock<IAuditRepository>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Theory]
        [InlineData("mock-patient-0001")]                        //census style identifier
        [InlineData("019d4eb6-6f02-72b7-b6dd-6cd10e161bac")]     //report style identifier
        [InlineData("Patient/e2e-999")]                          //relative FHIR reference
        public async Task ListAuditEvents_PassesIdentifierFiltersThroughUnchanged(string identifier)
        {
            var captured = new List<string?>();
            var controller = BuildController(captured);

            await controller.ListAuditEvents(null, null, null, null, null, null,
                patientId: identifier, resource: "Patient", resourceId: identifier, null, null);

            Assert.Equal(identifier, captured[0]);
            Assert.Equal("Patient", captured[1]);
            Assert.Equal(identifier, captured[2]);
        }

        [Fact]
        public async Task ListAuditEvents_StripsMarkupFromIdentifierFilters()
        {
            var captured = new List<string?>();
            var controller = BuildController(captured);

            await controller.ListAuditEvents(null, null, null, null, null, null,
                patientId: "<script>alert(1)</script>", resource: null, resourceId: null, null, null);

            Assert.DoesNotContain("<", captured[0]);
            Assert.DoesNotContain("script", captured[0]);
        }

        [Fact]
        public async Task ListAuditEvents_TreatsOmittedIdentifierFiltersAsNoFilter()
        {
            var captured = new List<string?>();
            var controller = BuildController(captured);

            await controller.ListAuditEvents(null, null, null, null, null, null,
                patientId: null, resource: null, resourceId: null, null, null);

            //the repository only applies a filter when it has length, so empty is "no filter"
            Assert.All(captured, x => Assert.True(string.IsNullOrEmpty(x)));
        }
    }
}
