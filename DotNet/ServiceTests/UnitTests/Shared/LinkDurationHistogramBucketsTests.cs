using LantanaGroup.Link.Shared.Application.Models.Telemetry;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class LinkDurationHistogramBucketsTests
{
    [Fact]
    public void Milliseconds_CoversOneMillisecondToSixtySeconds()
    {
        Assert.Equal(1, LinkDurationHistogramBuckets.Milliseconds[0]);
        Assert.Equal(60000, LinkDurationHistogramBuckets.Milliseconds[^1]);
        Assert.True(LinkDurationHistogramBuckets.Milliseconds[^1] >= 22900,
            "Buckets must cover the ~22.9 s Observation query so p95 is not +Inf.");
        Assert.Contains(DiagnosticNames.DataAcquisitionQueryDuration, LinkDurationHistogramBuckets.InstrumentNames);
        Assert.Contains(DiagnosticNames.DataAcquisitionSemaphoreWaitDuration, LinkDurationHistogramBuckets.InstrumentNames);
        Assert.Contains(DiagnosticNames.NormalizationDuration, LinkDurationHistogramBuckets.InstrumentNames);
        Assert.Contains(DiagnosticNames.ReportPersistDuration, LinkDurationHistogramBuckets.InstrumentNames);
        Assert.Contains(DiagnosticNames.TerminologyLookupDuration, LinkDurationHistogramBuckets.InstrumentNames);
        Assert.Contains("link_submission_upload_duration", LinkDurationHistogramBuckets.InstrumentNames);
        Assert.Contains("link_querydispatch_dispatch_duration", LinkDurationHistogramBuckets.InstrumentNames);
        Assert.DoesNotContain("link_submission_upload_size", LinkDurationHistogramBuckets.InstrumentNames);
    }
}
