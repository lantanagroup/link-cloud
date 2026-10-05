using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using FluentAssertions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PipelineDataReaderCancellationTests
{
    [Fact]
    public async Task GetAcquisitionLogsAsync_stops_before_the_next_page_when_cancelled()
    {
        var calls = 0;
        using var cts = new CancellationTokenSource();
        var dataAcq = new Mock<IDataAcquisitionServiceClient>();
        dataAcq
            .Setup(client => client.SearchAcquisitionLogsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                cts.Cancel();
                var records = Enumerable.Range(0, 100)
                    .Select(index => new DataAcquisitionLogApiModel { Id = index })
                    .ToList();
                return Task.FromResult(new LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>
                {
                    StatusCode = 200,
                    Body = new PagedConfigModel<DataAcquisitionLogApiModel>(
                        records,
                        new PaginationMetadata(100, 1, 200))
                });
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());

        var act = () => reader.GetAcquisitionLogsAsync("facility", "report", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetDataAcquisitionReportSummaryAsync_does_not_cache_a_result_after_cancellation()
    {
        var calls = 0;
        using var cts = new CancellationTokenSource();
        var dataAcq = new Mock<IDataAcquisitionServiceClient>();
        dataAcq
            .Setup(client => client.GetReportSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                {
                    cts.Cancel();
                    return Task.FromResult(new LinkApiResponse<DataAcquisitionReportSummaryApiModel>
                    {
                        StatusCode = 0
                    });
                }

                return Task.FromResult(new LinkApiResponse<DataAcquisitionReportSummaryApiModel>
                {
                    StatusCode = 200,
                    Body = new DataAcquisitionReportSummaryApiModel
                    {
                        ReportId = "report",
                        TotalLogs = 4
                    }
                });
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());

        var cancelled = () => reader.GetDataAcquisitionReportSummaryAsync("report", cts.Token);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();

        var summary = await reader.GetDataAcquisitionReportSummaryAsync("report", CancellationToken.None);
        calls.Should().Be(2);
        summary!.TotalLogs.Should().Be(4);
    }
}
