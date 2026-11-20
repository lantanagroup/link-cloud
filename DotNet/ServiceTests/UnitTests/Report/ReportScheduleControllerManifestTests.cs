using System.Linq.Expressions;
using System.Text;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application;
using LantanaGroup.Link.Report.Controllers;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// The ad-hoc manifest endpoint, <c>GET /api/schedules/{id}/manifest</c>. It returns whatever
/// <see cref="ReportManifestProducer.Generate"/> builds, skipping the completion gate, and must
/// never take the scheduled path that uploads and submits.
/// </summary>
[Trait("Category", "UnitTests")]
public class ReportScheduleControllerManifestTests
{
    [Fact]
    public async Task GetManifest_DefaultFormat_StreamsNdjsonOneResourcePerLine()
    {
        var harness = new Harness();

        var result = await harness.Controller.GetManifest(harness.Schedule.Id.ToString());

        Assert.IsType<EmptyResult>(result);
        Assert.Equal("application/x-ndjson", harness.HttpContext.Response.ContentType);
        var lines = harness.Body().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"resourceType\":\"Organization\"", lines[0]);
        Assert.Contains("\"resourceType\":\"OperationOutcome\"", lines[1]);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("JSON")]
    public async Task GetManifest_JsonFormat_ReturnsCollectionBundle(string format)
    {
        var harness = new Harness();

        var result = await harness.Controller.GetManifest(harness.Schedule.Id.ToString(), format);

        Assert.IsType<EmptyResult>(result);
        Assert.Equal("application/fhir+json", harness.HttpContext.Response.ContentType);
        var body = harness.Body();
        Assert.Contains("\"resourceType\":\"Bundle\"", body);
        Assert.Contains("\"type\":\"collection\"", body);
        Assert.Contains("\"resourceType\":\"Organization\"", body);
    }

    [Fact]
    public async Task GetManifest_UnsupportedFormat_Returns400()
    {
        var harness = new Harness();

        var result = await harness.Controller.GetManifest(harness.Schedule.Id.ToString(), "xml");

        Assert.IsType<BadRequestObjectResult>(result);
        harness.VerifyNotGenerated();
    }

    [Fact]
    public async Task GetManifest_MalformedId_Returns400()
    {
        var harness = new Harness();

        var result = await harness.Controller.GetManifest("not-a-guid");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid Id format", badRequest.Value);
        harness.VerifyNotGenerated();
    }

    [Fact]
    public async Task GetManifest_UnknownSchedule_Returns404()
    {
        var harness = new Harness(scheduleExists: false);

        var result = await harness.Controller.GetManifest(Guid.NewGuid().ToString());

        Assert.IsType<NotFoundResult>(result);
        harness.VerifyNotGenerated();
    }

    [Fact]
    public async Task GetManifest_LooksUpOnlySchedulesThatAreNotSoftDeleted()
    {
        var harness = new Harness();

        await harness.Controller.GetManifest(harness.Schedule.Id.ToString());

        var predicate = Assert.IsAssignableFrom<Expression<Func<ReportSchedule, bool>>>(harness.CapturedPredicate).Compile();
        Assert.True(predicate(new ReportSchedule { Id = harness.Schedule.Id, IsDeleted = null }));
        Assert.True(predicate(new ReportSchedule { Id = harness.Schedule.Id, IsDeleted = false }));
        Assert.False(predicate(new ReportSchedule { Id = harness.Schedule.Id, IsDeleted = true }));
        Assert.False(predicate(new ReportSchedule { Id = Guid.NewGuid(), IsDeleted = false }));
    }

    [Fact]
    public async Task GetManifest_PeriodNotClosed_Returns409()
    {
        var harness = new Harness(endOfReportPeriodJobHasRun: false);

        var result = await harness.Controller.GetManifest(harness.Schedule.Id.ToString());

        AssertProblem(result, StatusCodes.Status409Conflict);
        harness.VerifyNotGenerated();
    }

    [Fact]
    public async Task GetManifest_PeriodNotClosedButForced_Generates()
    {
        var harness = new Harness(endOfReportPeriodJobHasRun: false);

        var result = await harness.Controller.GetManifest(harness.Schedule.Id.ToString(), force: true);

        Assert.IsType<EmptyResult>(result);
        harness.Producer.Verify(p => p.Generate(harness.Schedule, ManifestGenerationMode.AdHoc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetManifest_FacilityConfigMissing_Returns409()
    {
        var harness = new Harness(generateThrows: new FacilityConfigNotFoundException("missing"));

        var result = await harness.Controller.GetManifest(harness.Schedule.Id.ToString());

        AssertProblem(result, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task GetManifest_TenantUnavailable_Returns502()
    {
        var harness = new Harness(generateThrows: new TenantUnavailableException("down"));

        var result = await harness.Controller.GetManifest(harness.Schedule.Id.ToString());

        AssertProblem(result, StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task GetManifest_UnexpectedFailure_Rethrows()
    {
        var harness = new Harness(generateThrows: new ApplicationException("boom"));

        await Assert.ThrowsAsync<ApplicationException>(
            () => harness.Controller.GetManifest(harness.Schedule.Id.ToString()));
    }

    /// <summary>
    /// The scheduled path uploads to blob storage and submits. The endpoint must never take it.
    /// </summary>
    [Fact]
    public async Task GetManifest_NeverTakesTheUploadAndSubmitPath()
    {
        var harness = new Harness();

        await harness.Controller.GetManifest(harness.Schedule.Id.ToString());

        harness.Producer.Verify(
            p => p.Produce(It.IsAny<ReportScheduleModel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.Producer.Verify(
            p => p.Generate(It.IsAny<ReportScheduleModel>(), ManifestGenerationMode.AdHoc, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Producer.Verify(
            p => p.Generate(It.IsAny<ReportScheduleModel>(), ManifestGenerationMode.Scheduled, It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ScheduleManager.Verify(
            m => m.UpdateAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static void AssertProblem(IActionResult result, int statusCode)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(statusCode, objectResult.StatusCode);
        Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    private sealed class Harness
    {
        public ReportScheduleModel Schedule { get; }
        public Mock<IReportScheduledManager> ScheduleManager { get; } = new();
        public Mock<ReportManifestProducer> Producer { get; } = ManifestProducerMocks.Create();
        public DefaultHttpContext HttpContext { get; } = new();
        public ReportScheduleController Controller { get; }
        public Expression<Func<ReportSchedule, bool>>? CapturedPredicate { get; private set; }

        public Harness(
            bool scheduleExists = true,
            bool endOfReportPeriodJobHasRun = true,
            Exception? generateThrows = null)
        {
            Schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid(),
                FacilityId = "facility-a",
                EndOfReportPeriodJobHasRun = endOfReportPeriodJobHasRun,
                Status = ScheduleStatus.EndOfPeriod
            };

            ScheduleManager
                .Setup(m => m.FindAsync(It.IsAny<Expression<Func<ReportSchedule, bool>>>(), It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<ReportSchedule, bool>>, CancellationToken>((expr, _) => CapturedPredicate = expr)
                .ReturnsAsync(scheduleExists ? [Schedule] : []);

            var generate = Producer.Setup(p => p.Generate(It.IsAny<ReportScheduleModel>(), It.IsAny<ManifestGenerationMode>(), It.IsAny<CancellationToken>()));
            if (generateThrows != null)
            {
                generate.ThrowsAsync(generateThrows);
            }
            else
            {
                generate.ReturnsAsync(
                [
                    new Organization { Id = "org-1", Name = "Facility A" },
                    new OperationOutcome
                    {
                        Id = "oo-1",
                        Issue =
                        [
                            new OperationOutcome.IssueComponent
                            {
                                Severity = OperationOutcome.IssueSeverity.Warning,
                                Code = OperationOutcome.IssueType.Incomplete,
                                Diagnostics = "Processing incomplete for patient p-1"
                            }
                        ]
                    }
                ]);
            }

            HttpContext.Response.Body = new MemoryStream();

            Controller = new ReportScheduleController(
                Mock.Of<ILogger<ReportScheduleController>>(),
                Mock.Of<IDatabase>(),
                ScheduleManager.Object,
                Producer.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = HttpContext }
            };
        }

        public string Body() =>
            Encoding.UTF8.GetString(((MemoryStream)HttpContext.Response.Body).ToArray());

        public void VerifyNotGenerated() =>
            Producer.Verify(p => p.Generate(It.IsAny<ReportScheduleModel>(), It.IsAny<ManifestGenerationMode>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>
/// A <see cref="ReportManifestProducer"/> whose virtual members can be set up. The constructor only
/// stores its arguments, so none of them are needed.
/// </summary>
internal static class ManifestProducerMocks
{
    public static Mock<ReportManifestProducer> Create() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
}
