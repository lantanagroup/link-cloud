using Automation.UI.Services;
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
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
    public async Task GetAcquisitionLogsAsync_does_not_return_earlier_pages_when_a_later_page_is_cancelled()
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                {
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
                }

                cts.Cancel();
                return Task.FromResult(new LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>
                {
                    StatusCode = 0
                });
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());

        var act = () => reader.GetAcquisitionLogsAsync("facility", "report", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(2);
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

    [Fact]
    public async Task Search_shaped_logs_keep_a_zero_duration_and_load_notes_only_for_the_failure()
    {
        var completed = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>
            {
                StatusCode = 200,
                Body = new PagedConfigModel<DataAcquisitionLogApiModel>(
                    [
                        new DataAcquisitionLogApiModel
                        {
                            Id = 11,
                            Status = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus.Completed,
                            CompletionTimeMilliseconds = 0,
                            CompletionDate = completed,
                            Notes = null,
                            ResourceTypes = ["Observation"]
                        },
                        new DataAcquisitionLogApiModel
                        {
                            Id = 12,
                            Status = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus.Failed,
                            CompletionTimeMilliseconds = 100,
                            CompletionDate = completed,
                            Notes = null,
                            ResourceTypes = ["Encounter"]
                        }
                    ],
                    new PaginationMetadata(100, 1, 2))
            });
        dataAcq
            .Setup(client => client.GetAcquisitionLogNotesAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<string>>
            {
                StatusCode = 200,
                Body = ["gave up"]
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());

        var logs = await reader.GetAcquisitionLogsAsync("facility", "report");
        logs.Should().OnlyContain(log => log.Notes.Count == 0);
        logs.Select(log => log.CompletionTimeMilliseconds).Should().Equal(0L, 100L);

        var withNotes = await reader.AttachFailureNotesAsync(logs, RunHistorySlim.FailureSampleIds(logs));
        var chart = RunHistorySlim.ToAcquisitionChart(withNotes);

        chart.MinDurationMs.Should().Be(0);
        chart.AverageDurationMs.Should().Be(50);
        chart.MaxDurationMs.Should().Be(100);
        chart.Failures.Should().ContainSingle().Which.Message.Should().Be("gave up");
        dataAcq.Verify(client => client.GetAcquisitionLogNotesAsync(12, It.IsAny<CancellationToken>()), Times.Once);
        dataAcq.Verify(client => client.GetAcquisitionLogNotesAsync(11, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AttachFailureNotesAsync_keeps_other_notes_when_one_lookup_throws()
    {
        var dataAcq = new Mock<IDataAcquisitionServiceClient>();
        dataAcq
            .Setup(client => client.GetAcquisitionLogNotesAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("notes failed"));
        dataAcq
            .Setup(client => client.GetAcquisitionLogNotesAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<List<string>> { StatusCode = 200, Body = ["kept"] });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var logs = new List<PipelineDataReader.AcquisitionLogInfo>
        {
            new(1, null, null, null, "Failed", null, [], [], []),
            new(2, null, null, null, "Failed", null, [], [], [])
        };

        var withNotes = await reader.AttachFailureNotesAsync(logs, [1, 2]);

        withNotes.Single(log => log.Id == 1).Notes.Should().BeEmpty();
        withNotes.Single(log => log.Id == 2).Notes.Should().Equal("kept");
    }

    [Fact]
    public async Task AttachFailureNotesAsync_still_stops_when_the_caller_cancels()
    {
        using var cts = new CancellationTokenSource();
        var dataAcq = new Mock<IDataAcquisitionServiceClient>();
        dataAcq
            .Setup(client => client.GetAcquisitionLogNotesAsync(1, It.IsAny<CancellationToken>()))
            .Returns((long _, CancellationToken token) =>
            {
                cts.Cancel();
                return Task.FromException<LinkApiResponse<List<string>>>(new OperationCanceledException(token));
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var logs = new List<PipelineDataReader.AcquisitionLogInfo>
        {
            new(1, null, null, null, "Failed", null, [], [], [])
        };

        var act = () => reader.AttachFailureNotesAsync(logs, [1], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AttachFailureNotesAsync_stops_when_a_cancelled_lookup_returns_no_body()
    {
        using var cts = new CancellationTokenSource();
        var dataAcq = new Mock<IDataAcquisitionServiceClient>();
        dataAcq
            .Setup(client => client.GetAcquisitionLogNotesAsync(1, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cts.Cancel();
                return Task.FromResult(new LinkApiResponse<List<string>>
                {
                    StatusCode = 0
                });
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var logs = new List<PipelineDataReader.AcquisitionLogInfo>
        {
            new(1, null, null, null, "Failed", null, [], [], [])
        };

        var act = () => reader.AttachFailureNotesAsync(logs, [1], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetAcquisitionLogsAsync_rejects_a_bodyless_page_without_returning_earlier_pages()
    {
        var calls = 0;
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                {
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
                }

                return Task.FromResult(new LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>
                {
                    StatusCode = 500
                });
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());

        var act = () => reader.GetAcquisitionLogsAsync("facility", "report");

        await act.Should().ThrowAsync<HttpRequestException>();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAcquisitionLogsAsync_stops_after_a_successful_empty_page()
    {
        var calls = 0;
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
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                {
                    var records = Enumerable.Range(0, 100)
                        .Select(index => new DataAcquisitionLogApiModel { Id = index + 1 })
                        .ToList();
                    return Task.FromResult(new LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>
                    {
                        StatusCode = 200,
                        Body = new PagedConfigModel<DataAcquisitionLogApiModel>(
                            records,
                            new PaginationMetadata(100, 1, 150))
                    });
                }

                return Task.FromResult(new LinkApiResponse<PagedConfigModel<DataAcquisitionLogApiModel>>
                {
                    StatusCode = 200,
                    Body = new PagedConfigModel<DataAcquisitionLogApiModel>(
                        [],
                        new PaginationMetadata(100, 2, 1))
                });
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());

        var logs = await reader.GetAcquisitionLogsAsync("facility", "report");

        logs.Should().HaveCount(100);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task AttachFailureNotesAsync_reuses_a_successful_lookup_and_retries_a_failed_one()
    {
        var calls = 0;
        var dataAcq = new Mock<IDataAcquisitionServiceClient>();
        dataAcq
            .Setup(client => client.GetAcquisitionLogNotesAsync(1, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 1)
                {
                    return Task.FromResult(new LinkApiResponse<List<string>>
                    {
                        StatusCode = 500
                    });
                }

                return Task.FromResult(new LinkApiResponse<List<string>>
                {
                    StatusCode = 200,
                    Body = ["kept"]
                });
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            dataAcq.Object,
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var logs = new List<PipelineDataReader.AcquisitionLogInfo>
        {
            new(1, null, null, null, "Failed", null, [], [], [])
        };

        var first = await reader.AttachFailureNotesAsync(logs, [1]);
        var second = await reader.AttachFailureNotesAsync(logs, [1]);
        var third = await reader.AttachFailureNotesAsync(logs, [1]);

        first.Single().Notes.Should().BeEmpty();
        second.Single().Notes.Should().Equal("kept");
        third.Single().Notes.Should().Equal("kept");
        calls.Should().Be(2);
    }
}
