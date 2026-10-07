using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class LogsRulesTests
{
    private static readonly DateTime Noon = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Process_skips_completed_and_skipped_only()
    {
        LogsRules.CanProcess("Completed").Should().BeFalse();
        LogsRules.CanProcess("Skipped").Should().BeFalse();
        LogsRules.CanProcess("Failed").Should().BeTrue();
        LogsRules.CanProcess("Cancelled").Should().BeTrue();
        LogsRules.CanProcess(" ").Should().BeFalse();
    }

    [Fact]
    public void Cancel_treats_an_unspecified_created_time_as_utc_and_blocks_terminal_statuses()
    {
        var created = new DateTime(2026, 10, 6, 11, 0, 0, DateTimeKind.Unspecified);
        LogsRules.CanCancel("Pending", created, 24, Noon).Should().BeTrue();
        LogsRules.CanCancel("Pending", created, 26, Noon).Should().BeFalse();
        LogsRules.CanCancel("Pending", created, 0, Noon).Should().BeTrue();
        LogsRules.CanCancel("Cancelled", created, 0, Noon).Should().BeFalse();
        LogsRules.CanCancel("Completed", created, 0, Noon).Should().BeFalse();
        LogsRules.CanCancel("Ready", null, 24, Noon).Should().BeFalse();
    }

    [Fact]
    public void Sftp_reset_is_only_configuration_required_or_max_retries()
    {
        LogsRules.CanResetSftp("ConfigurationRequired").Should().BeTrue();
        LogsRules.CanResetSftp("maxretriesreached").Should().BeTrue();
        LogsRules.CanResetSftp("Failed").Should().BeFalse();
        LogsRules.CanResetSftp(null).Should().BeFalse();
    }

    [Fact]
    public void External_links_are_absolute_http_or_https_only()
    {
        LogsRules.TryExternalUrl("javascript:alert(1)", out _).Should().BeFalse();
        LogsRules.TryExternalUrl("/kafka", out _).Should().BeFalse();
        LogsRules.TryExternalUrl("ftp://files.example/ui", out _).Should().BeFalse();
        LogsRules.TryExternalUrl("https://kafka.example/ui", out var url).Should().BeTrue();
        url.Should().Be("https://kafka.example/ui");
    }

    [Fact]
    public void Cancellable_only_replaces_statuses_and_sets_a_created_before()
    {
        var search = LogsRules.Prepare(new AcquisitionQuery { CancellableOnly = true, MinAgeHours = 24 }, false, Noon);
        search.Error.Should().BeNull();
        search.Statuses.Should().Equal(LogsRules.CancellableStatuses);
        search.CreatedBefore.Should().Be(Noon.AddHours(-24));
        search.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void Include_deleted_alone_is_not_a_narrowing_filter()
    {
        var search = LogsRules.Prepare(new AcquisitionQuery { IncludeDeleted = true }, false, Noon);
        search.HasFilter.Should().BeFalse();
        LogsRules.Prepare(new AcquisitionQuery(), false, Noon).HasFilter.Should().BeFalse();
        LogsRules.Prepare(new AcquisitionQuery { FacilityId = "facility-1" }, false, Noon).HasFilter.Should().BeTrue();
    }

    [Fact]
    public void Unknown_acquisition_filters_are_errors_and_page_size_is_clamped()
    {
        LogsRules.Prepare(new AcquisitionQuery { ResourceType = "Nope" }, false, Noon).Error
            .Should().Contain("resource type");
        LogsRules.Prepare(new AcquisitionQuery { Priority = "Urgent" }, false, Noon).Error
            .Should().Contain("Priority");
        LogsRules.Prepare(new AcquisitionQuery { Status = ["Nope"] }, false, Noon).Error
            .Should().Contain("Status");
        LogsRules.ClampPageSize(15).Should().Be(LogsRules.DefaultPageSize);
        LogsRules.ClampMinAge(-3).Should().Be(0);
        LogsRules.ClampMinAge(99999).Should().Be(LogsRules.MaxMinAgeHours);
    }

    [Fact]
    public void Selected_ids_stop_at_fifty()
    {
        LogsRules.ParseIds([0, -1], out var error).Should().BeEmpty();
        error.Should().Be("Select at least one log.");

        var ids = Enumerable.Range(1, 51).Select(value => (long)value).ToArray();
        LogsRules.ParseIds(ids, out error).Should().BeEmpty();
        error.Should().Contain("50");
    }

    [Fact]
    public void Audit_page_size_stops_at_twenty_and_submission_is_not_an_action()
    {
        var search = LogsRules.PrepareAudit(new AuditQuery(), false);
        search.SortBy.Should().Be("CreatedOn");
        search.SortDir.Should().Be("desc");
        search.PageSize.Should().Be(10);
        search.Error.Should().BeNull();

        LogsRules.PrepareAudit(new AuditQuery { PageSize = 50 }, false).PageSize.Should().Be(10);
        LogsRules.ClampAuditPageSize(20).Should().Be(20);
        LogsRules.PrepareAudit(new AuditQuery { Action = "Submit" }, false).Action.Should().Be("Submit");
        LogsRules.PrepareAudit(new AuditQuery { Action = "Submission" }, false).Error
            .Should().Contain("Action");
    }

    [Fact]
    public void Sftp_rejects_an_unknown_type_and_does_not_offer_include_deleted()
    {
        LogsRules.PrepareSftp(new SftpQuery { AcquisitionType = "Census" }, false).AcquisitionType.Should().Be("Census");
        LogsRules.PrepareSftp(new SftpQuery { AcquisitionType = "Other" }, false).Error
            .Should().Contain("Acquisition type");
        LogsRules.PrepareSftp(new SftpQuery { Status = "Nope" }, false).Error
            .Should().Contain("Status");
        typeof(SftpQuery).GetProperty("IncludeDeleted").Should().BeNull();
    }
}
