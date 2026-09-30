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

    private sealed class CapturingOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
