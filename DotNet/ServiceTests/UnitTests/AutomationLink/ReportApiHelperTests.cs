using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Services;

namespace UnitTests.AutomationLink;

[Trait("Category", "UnitTests")]
public class ReportApiHelperTests
{
    private const string TrackingId = "131e5af2-fdeb-47c9-8e51-580aafc0f124";

    [Fact]
    public void NotPersisted_names_the_lookup_and_where_to_look()
    {
        var message = ReportApiHelper.DescribeScheduledReportNotPersisted(
            TrackingId,
            TimeSpan.FromSeconds(120),
            lastStatusCode: 404,
            lastBody: "{\"title\":\"Not Found\"}");

        message.Should().Contain(TrackingId);
        message.Should().Contain("within 120s");
        message.Should().Contain("HTTP 404");
        message.Should().Contain($"GET /api/schedules/{TrackingId}");
        message.Should().Contain("when that facility or that report id is aborted");
        message.Should().Contain("ReportScheduled error topic");
        message.Should().Contain("ReportScheduledListener");
        message.Should().Contain("Not Found");
        message.Should().NotContain("<");
    }

    [Fact]
    public void NotPersisted_says_no_response_when_the_lookup_never_returned()
    {
        var message = ReportApiHelper.DescribeScheduledReportNotPersisted(
            TrackingId,
            TimeSpan.FromMinutes(2),
            lastStatusCode: null,
            lastBody: null);

        message.Should().Contain("within 120s");
        message.Should().Contain("last response was no response");
        message.Should().NotContain("Response:");
    }

    [Fact]
    public void NotPersisted_strips_markup_and_caps_the_body()
    {
        var body = "<script>alert(1)</script>" + new string('x', 400);
        var message = ReportApiHelper.DescribeScheduledReportNotPersisted(
            TrackingId,
            TimeSpan.FromSeconds(120),
            lastStatusCode: 500,
            lastBody: body);

        message.Should().NotContain("<script");
        message.Should().NotContain("alert(1)");
        message.Should().NotContain(new string('x', 181));
        message.Should().Contain("Response: ");
        message.Should().Contain(new string('x', 180));
    }

    [Fact]
    public void NotPersisted_says_when_the_facility_is_still_aborted()
    {
        var message = ReportApiHelper.DescribeScheduledReportNotPersisted(
            TrackingId,
            TimeSpan.FromSeconds(120),
            lastStatusCode: 404,
            lastBody: null,
            facilityId: "10765",
            facilityAborted: true);

        message.Should().Contain("Facility '10765' is still aborted");
        message.Should().Contain("does not store the schedule");
        message.Should().Contain("within 120s");
    }

    [Fact]
    public void NotPersisted_says_when_the_facility_abort_flag_was_clear()
    {
        var message = ReportApiHelper.DescribeScheduledReportNotPersisted(
            TrackingId,
            TimeSpan.FromSeconds(120),
            lastStatusCode: 404,
            lastBody: null,
            facilityId: "22001",
            facilityAborted: false);

        message.Should().Contain("The facility abort flag was not set when the wait ended.");
        message.Should().NotContain("still aborted");
    }

    [Fact]
    public void NotPersisted_strips_markup_from_the_facility_id()
    {
        var message = ReportApiHelper.DescribeScheduledReportNotPersisted(
            TrackingId,
            TimeSpan.FromSeconds(120),
            lastStatusCode: 404,
            lastBody: null,
            facilityId: "22001<script>",
            facilityAborted: true);

        message.Should().Contain("still aborted");
        message.Should().NotContain("<script");
        message.Should().NotContain("<");
    }
}
