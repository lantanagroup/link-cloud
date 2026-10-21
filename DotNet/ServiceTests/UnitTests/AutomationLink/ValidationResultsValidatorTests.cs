using FluentAssertions;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Moq;

namespace UnitTests.AutomationLink;

[Trait("Category", "UnitTests")]
public class ValidationResultsValidatorTests
{
    [Fact]
    public async Task AvailabilityCheck_UsesSummaryAndDoesNotDownloadTheResultList()
    {
        using var cts = new CancellationTokenSource();
        var client = new Mock<IValidationServiceClient>(MockBehavior.Strict);
        client.Setup(c => c.GetValidationResultSummaryAsync(
                "facility-1",
                "report-1",
                "WARNING",
                cts.Token))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 200,
                Body = """{"count":6010,"severity":"WARNING"}"""
            });

        SetupNoCategoryCounts(client);

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        await sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"], cancellationToken: cts.Token);

        output.Lines.Should().Contain("VALIDATION RESULTS (API): Passed");
        client.Verify(c => c.GetValidationResultsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AvailabilityCheck_WhenSummaryFails_ReportsStatusAndDoesNotDownloadTheResultList()
    {
        var client = new Mock<IValidationServiceClient>(MockBehavior.Strict);
        client.Setup(c => c.GetValidationResultSummaryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "WARNING",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 500,
                RawBody = "validation unavailable"
            });

        SetupNoCategoryCounts(client);

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        var act = () => sut.ValidateAllAsync("facility-1", "report-1", []);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*VALIDATION RESULTS (API) failed with 1 issue(s)*");
        output.Lines.Should().Contain(line => line.Contains("HTTP 500: validation unavailable", StringComparison.Ordinal));
        client.Verify(c => c.GetValidationResultsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AvailabilityCheck_OutOfMemoryFromTheResultListIsNotTheProbe()
    {
        var client = new Mock<IValidationServiceClient>(MockBehavior.Strict);
        client.Setup(c => c.GetValidationResultsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutOfMemoryException());
        client.Setup(c => c.GetValidationResultSummaryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "WARNING",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string> { StatusCode = 200, Body = """{"count":1,"severity":"WARNING"}""" });

        SetupNoCategoryCounts(client);

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        await sut.ValidateAllAsync("facility-1", "report-1", []);

        output.Lines.Should().Contain("VALIDATION RESULTS (API): Passed");
        output.Lines.Should().NotContain(line => line.Contains("OutOfMemoryException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AvailabilityCheck_WhenBodyIsAJsonArray_FailsInsteadOfPassing()
    {
        var client = new Mock<IValidationServiceClient>(MockBehavior.Strict);
        client.Setup(c => c.GetValidationResultSummaryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "WARNING",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string> { StatusCode = 200, Body = "[]" });

        SetupNoCategoryCounts(client);

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        var act = () => sut.ValidateAllAsync("facility-1", "report-1", []);

        await act.Should().ThrowAsync<InvalidOperationException>();
        output.Lines.Should().Contain(line => line.Contains("not a result summary", StringComparison.Ordinal));
        output.Lines.Should().Contain(line => line.Contains("[]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(0)]
    public async Task AvailabilityCheck_WhenTheRunIsCancelled_ThrowsInsteadOfFailingTheCheck(int statusCode)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = new Mock<IValidationServiceClient>(MockBehavior.Strict);
        client.Setup(c => c.GetValidationResultSummaryAsync(
                "facility-1",
                "report-1",
                "WARNING",
                cts.Token))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = statusCode,
                Body = statusCode == 200 ? """{"count":1,"severity":"WARNING"}""" : null,
                RawBody = statusCode == 200 ? null : ""
            });

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        var act = () => sut.ValidateAllAsync("facility-1", "report-1", [], cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        output.Lines.Should().NotContain(line => line.Contains("VALIDATION RESULTS (API): Failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CategoryCounts_MatchingSavedFindings_Passes()
    {
        var client = ClientWithCounts(
            savedFindings: 10,
            storedCounts: Counts(("category-a", 7, true), ("category-n", 4, false), ("uncategorized", 3, true)),
            liveCounts: Live(("category-a", 7), ("category-n", 2)));

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        await sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"]);

        output.Lines.Should().Contain("VALIDATION CATEGORY COUNTS: 3 categories match the 10 saved findings");
        output.Lines.Should().Contain("VALIDATION RESULTS (API): Passed");
    }

    [Fact]
    public async Task CategoryCounts_CategoryIdsDifferingOnlyByCase_AreOneCategory()
    {
        var client = ClientWithCounts(
            savedFindings: 7,
            storedCounts: Counts(("category-a", 7, true)),
            liveCounts: Live(("CATEGORY-A", 5), ("category-a", 2)));

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        await sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"]);

        output.Lines.Should().Contain("VALIDATION RESULTS (API): Passed");
    }

    [Fact]
    public async Task CategoryCounts_SubmittedCategoryDiffersFromSavedFindings_Fails()
    {
        var client = ClientWithCounts(
            savedFindings: 7,
            storedCounts: Counts(("category-a", 7, true)),
            liveCounts: Live(("category-a", 6)));

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        var act = () => sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        output.Lines.Should().Contain(line => line.Contains(
            "'category-a' (submitted): stored count 7, saved findings 6",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task CategoryCounts_NonSubmittedCategoryBelowSavedFindings_Fails()
    {
        var client = ClientWithCounts(
            savedFindings: 2,
            storedCounts: Counts(("category-n", 1, false)),
            liveCounts: Live(("category-n", 2)));

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        var act = () => sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        output.Lines.Should().Contain(line => line.Contains("'category-n' (not submitted)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CategoryCounts_SavedCategoryWithNoStoredCount_Fails()
    {
        var client = ClientWithCounts(
            savedFindings: 3,
            storedCounts: Counts(("category-a", 2, true)),
            liveCounts: Live(("category-a", 2), ("category-b", 1)));

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        var act = () => sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        output.Lines.Should().Contain(line => line.Contains(
            "'category-b' has 1 saved finding(s) but no stored count",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task CategoryCounts_MissingWhileFindingsAreSaved_Fails()
    {
        var client = ClientWithCounts(savedFindings: 5, storedCounts: null, liveCounts: null);

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        var act = () => sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        output.Lines.Should().Contain(line => line.Contains(
            "counts are missing although the report has 5 saved finding(s)",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task CategoryCounts_AboveTheComparisonLimit_SkipsTheLiveCount()
    {
        var client = ClientWithCounts(
            savedFindings: ValidationResultsValidator.CategoryCountComparisonLimit + 1,
            storedCounts: Counts(("category-a", 7, true)),
            liveCounts: null);

        var output = new CapturingOutput();
        var sut = new ValidationResultsValidator(client.Object, output);

        await sut.ValidateAllAsync("facility-1", "report-1", ["patient-1"]);

        output.Lines.Should().Contain(line => line.Contains("comparison with the", StringComparison.Ordinal) &&
                                              line.Contains("skipped", StringComparison.Ordinal));
        output.Lines.Should().Contain("VALIDATION RESULTS (API): Passed");
        client.Verify(c => c.GetReportCategorySummariesAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A report with no saved findings and no stored counts, so the category count check has nothing to compare.
    /// </summary>
    private static void SetupNoCategoryCounts(Mock<IValidationServiceClient> client)
    {
        client.Setup(c => c.GetValidationResultSummaryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "INFORMATION",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 200,
                Body = """{"count":0,"severity":"INFORMATION"}"""
            });
        client.Setup(c => c.GetReportCategoryCountsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string> { StatusCode = 404, RawBody = "{}" });
    }

    /// <summary>
    /// A client whose WARNING summary passes, with the given saved findings, stored counts (null means 404) and
    /// live counts (null means not set up, so the strict mock fails if they are requested).
    /// </summary>
    private static Mock<IValidationServiceClient> ClientWithCounts(long savedFindings,
                                                                  string? storedCounts,
                                                                  string? liveCounts)
    {
        var client = new Mock<IValidationServiceClient>(MockBehavior.Strict);
        client.Setup(c => c.GetValidationResultSummaryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "WARNING",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 200,
                Body = """{"count":1,"severity":"WARNING"}"""
            });
        client.Setup(c => c.GetValidationResultSummaryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "INFORMATION",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 200,
                Body = "{\"count\":" + savedFindings + ",\"severity\":\"INFORMATION\"}"
            });
        client.Setup(c => c.GetReportCategoryCountsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedCounts is null
                ? new LinkApiResponse<string> { StatusCode = 404, RawBody = "{}" }
                : new LinkApiResponse<string> { StatusCode = 200, Body = storedCounts });
        if (liveCounts is not null)
        {
            client.Setup(c => c.GetReportCategorySummariesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LinkApiResponse<string> { StatusCode = 200, Body = liveCounts });
        }

        return client;
    }

    private static string Counts(params (string CategoryId, long Issues, bool Submit)[] categories)
    {
        var items = categories.Select(c =>
            "{\"categoryId\":\"" + c.CategoryId + "\",\"title\":\"" + c.CategoryId + "\",\"acceptable\":false," +
            "\"submit\":" + (c.Submit ? "true" : "false") + ",\"issues\":" + c.Issues + "}");
        return "{\"facilityId\":\"facility-1\",\"reportId\":\"report-1\",\"lastUpdated\":\"2026-10-21T12:00:00Z\"," +
               "\"categories\":[" + string.Join(",", items) + "]}";
    }

    private static string Live(params (string CategoryId, long Issues)[] categories)
    {
        var items = categories.Select(c =>
            "{\"categoryId\":\"" + c.CategoryId + "\",\"issues\":" + c.Issues + ",\"acceptable\":false}");
        return "[" + string.Join(",", items) + "]";
    }


    private sealed class CapturingOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
