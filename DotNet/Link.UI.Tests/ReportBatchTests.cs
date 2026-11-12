using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Link.UI.Controllers;
using Link.UI.Controllers.Api;
using Link.UI.Models;
using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Link.UI.Tests;

public class ReportBatchTests
{
    [Fact]
    public async Task The_cap_defaults_to_two_until_a_value_is_saved()
    {
        var settings = new MemorySettings();
        (await settings.GetMaxConcurrencyAsync()).Should().Be(2);

        var harness = Harness(settings);
        var batch = await harness.Dispatcher.StartBatchAsync("local", Ids(harness, 3), null, CancellationToken.None);

        batch.MaxConcurrency.Should().Be(2);
        harness.Runs.Started.Should().HaveCount(2);
        batch.Items.Count(item => item.Status == ReportBatchItemStatus.Running).Should().Be(2);
        batch.Items.Count(item => item.Status == ReportBatchItemStatus.Queued).Should().Be(1);
        batch.Status.Should().Be(ReportBatchStatus.Running);
    }

    [Fact]
    public async Task Saving_the_cap_is_what_the_next_read_returns()
    {
        var settings = new MemorySettings();
        await settings.SaveMaxConcurrencyAsync(4);

        (await settings.GetMaxConcurrencyAsync()).Should().Be(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9)]
    public async Task The_saved_cap_must_be_from_one_to_eight(int value)
    {
        var settings = new MemorySettings();
        var act = () => settings.SaveMaxConcurrencyAsync(value);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await settings.GetMaxConcurrencyAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_batch_may_lower_the_cap_and_cannot_raise_it()
    {
        var settings = new MemorySettings();
        await settings.SaveMaxConcurrencyAsync(4);
        var harness = Harness(settings);

        var lowered = await harness.Dispatcher.StartBatchAsync("local", Ids(harness, 3), 1, CancellationToken.None);
        lowered.MaxConcurrency.Should().Be(1);
        harness.Runs.Started.Should().HaveCount(1);

        var act = () => harness.Dispatcher.StartBatchAsync("local", Ids(harness, 1), 5, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*saved cap of 4*");
    }

    [Fact]
    public async Task A_saved_cap_applies_to_the_next_batch_and_not_the_one_already_dispatched()
    {
        var settings = new MemorySettings();
        var harness = Harness(settings);
        var first = await harness.Dispatcher.StartBatchAsync("local", Ids(harness, 3), null, CancellationToken.None);
        first.MaxConcurrency.Should().Be(2);
        harness.Runs.Started.Should().HaveCount(2);

        await settings.SaveMaxConcurrencyAsync(1);
        var secondScenarios = Enumerable.Range(0, 2).Select(_ => harness.AddScenario("later")).ToList();
        var second = await harness.Dispatcher.StartBatchAsync(
            "local",
            secondScenarios.Select(scenario => scenario.Id).ToList(),
            null,
            CancellationToken.None);

        second.MaxConcurrency.Should().Be(1);
        second.Items.Count(item => item.Status == ReportBatchItemStatus.Running).Should().Be(1);
        first.MaxConcurrency.Should().Be(2);
        first.Items.Count(item => item.Status == ReportBatchItemStatus.Running).Should().Be(2);
        harness.Runs.Started.Should().HaveCount(3);
    }

    [Fact]
    public async Task The_queued_run_starts_when_a_slot_frees()
    {
        var harness = Harness();
        var batch = await harness.Dispatcher.StartBatchAsync("local", Ids(harness, 3), null, CancellationToken.None);
        var running = batch.Items.Single(item => item.Ordinal == 0);
        harness.Runs.Status[running.RunId!.Value] = AutomationRunStatus.Succeeded;

        await harness.Dispatcher.PumpAsync(CancellationToken.None);

        var stored = await harness.Batches.GetAsync(batch.BatchId);
        stored!.Items.Single(item => item.Ordinal == 2).Status.Should().Be(ReportBatchItemStatus.Running);
        harness.Runs.Started.Should().HaveCount(3);
        stored.Items.Select(item => item.RunId).Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task A_failed_start_does_not_stop_the_other_runs()
    {
        var settings = new MemorySettings();
        await settings.SaveMaxConcurrencyAsync(3);
        var harness = Harness(settings);
        var ids = Ids(harness, 3);
        harness.Runs.FailScenarioIds.Add(ids[1]);

        var batch = await harness.Dispatcher.StartBatchAsync("local", ids, 3, CancellationToken.None);

        batch.Items[1].Status.Should().Be(ReportBatchItemStatus.Failed);
        batch.Items[0].Status.Should().Be(ReportBatchItemStatus.Running);
        batch.Items[2].Status.Should().Be(ReportBatchItemStatus.Running);
        harness.Runs.Started.Should().HaveCount(2);
        batch.Status.Should().Be(ReportBatchStatus.Running);
    }

    [Fact]
    public async Task A_run_that_fails_does_not_stop_the_queued_run()
    {
        var harness = Harness();
        var batch = await harness.Dispatcher.StartBatchAsync("local", Ids(harness, 2), 1, CancellationToken.None);
        harness.Runs.Status[batch.Items[0].RunId!.Value] = AutomationRunStatus.Failed;

        await harness.Dispatcher.PumpAsync(CancellationToken.None);

        var stored = await harness.Batches.GetAsync(batch.BatchId);
        stored!.Items[0].Status.Should().Be(ReportBatchItemStatus.Failed);
        stored.Items[1].Status.Should().Be(ReportBatchItemStatus.Running);
        harness.Runs.Started.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_metrics_run_in_the_batch_waits_for_the_batches_own_metrics_run()
    {
        var harness = Harness();
        var first = harness.AddScenario("metrics-a", metrics: true);
        var normal = harness.AddScenario("normal");
        var second = harness.AddScenario("metrics-b", metrics: true);

        var batch = await harness.Dispatcher.StartBatchAsync(
            "local",
            [first.Id, normal.Id, second.Id],
            2,
            CancellationToken.None);

        batch.Items[0].Status.Should().Be(ReportBatchItemStatus.Running);
        batch.Items[1].Status.Should().Be(ReportBatchItemStatus.Running);
        batch.Items[2].Status.Should().Be(ReportBatchItemStatus.Queued);
        harness.Runs.Started.Select(request => request.ScenarioId).Should().Equal(first.Id, normal.Id);

        harness.Runs.Status[batch.Items[0].RunId!.Value] = AutomationRunStatus.Succeeded;
        await harness.Dispatcher.PumpAsync(CancellationToken.None);

        var stored = await harness.Batches.GetAsync(batch.BatchId);
        stored!.Items[2].Status.Should().Be(ReportBatchItemStatus.Running);
        harness.Runs.Started.Should().HaveCount(3);
        harness.Runs.Started.Should().OnlyContain(request => request.EhrSystem == "local");
    }

    [Fact]
    public async Task A_metrics_refusal_from_outside_the_batch_fails_that_item_only()
    {
        var harness = Harness();
        harness.Runs.ThrowMetricsRefusal = true;
        var metrics = harness.AddScenario("metrics", metrics: true);
        var normal = harness.AddScenario("normal");

        var batch = await harness.Dispatcher.StartBatchAsync("local", [metrics.Id, normal.Id], 2, CancellationToken.None);

        batch.Items[0].Status.Should().Be(ReportBatchItemStatus.Failed);
        batch.Items[0].Error.Should().Be(ParallelRunSafetyMessage);
        batch.Items[1].Status.Should().Be(ReportBatchItemStatus.Running);
        harness.Runs.Started.Should().ContainSingle();
        harness.Runs.Started[0].ScenarioId.Should().Be(normal.Id);
    }

    [Fact]
    public async Task Cancel_does_not_start_queued_runs_and_cancels_the_running_one()
    {
        var harness = Harness();
        var batch = await harness.Dispatcher.StartBatchAsync("local", Ids(harness, 3), 1, CancellationToken.None);
        var runningId = batch.Items[0].RunId!.Value;

        var cancelled = await harness.Dispatcher.CancelBatchAsync(batch.BatchId, CancellationToken.None);

        cancelled!.Status.Should().Be(ReportBatchStatus.Cancelled);
        cancelled.Items[0].Status.Should().Be(ReportBatchItemStatus.Cancelled);
        cancelled.Items[1].Status.Should().Be(ReportBatchItemStatus.Cancelled);
        cancelled.Items[1].Error.Should().Be(ReportBatchDispatcher.CancelledBeforeStart);
        cancelled.Items[2].Error.Should().Be(ReportBatchDispatcher.CancelledBeforeStart);
        harness.Runs.Cancelled.Should().Equal(runningId);
        harness.Runs.Started.Should().ContainSingle();
    }

    [Fact]
    public async Task Restart_does_not_resume_an_in_flight_run_and_does_start_one_that_never_started()
    {
        var harness = Harness();
        var queued = harness.AddScenario("queued");
        var runId = Guid.NewGuid();
        harness.Runs.Status[runId] = AutomationRunStatus.Running;
        await harness.Batches.InsertAsync(new ReportBatchDocument
        {
            BatchId = Guid.NewGuid(),
            EhrSystem = "local",
            MaxConcurrency = 2,
            Status = ReportBatchStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new ReportBatchItemDocument
                {
                    Ordinal = 0,
                    ScenarioId = Guid.NewGuid(),
                    ScenarioName = "in flight",
                    Status = ReportBatchItemStatus.Running,
                    RunId = runId
                },
                new ReportBatchItemDocument
                {
                    Ordinal = 1,
                    ScenarioId = queued.Id,
                    ScenarioName = queued.Name,
                    Status = ReportBatchItemStatus.Queued
                }
            ]
        });

        await harness.Dispatcher.RecoverAsync(CancellationToken.None);
        var afterRecovery = (await harness.Batches.ListActiveAsync()).Single();
        afterRecovery.Items[0].Status.Should().Be(ReportBatchItemStatus.Cancelled);
        afterRecovery.Items[0].Error.Should().Be(ReportBatchDispatcher.InterruptedByRestart);
        afterRecovery.Items[1].Status.Should().Be(ReportBatchItemStatus.Queued);
        harness.Runs.Cancelled.Should().Equal(runId);
        harness.Runs.Started.Should().BeEmpty();

        await harness.Dispatcher.PumpAsync(CancellationToken.None);

        var stored = (await harness.Batches.ListActiveAsync()).Single();
        stored.Items[1].Status.Should().Be(ReportBatchItemStatus.Running);
        harness.Runs.Started.Should().ContainSingle();
        harness.Runs.Started[0].ScenarioId.Should().Be(queued.Id);
        harness.Runs.Started[0].EhrSystem.Should().Be("local");
    }

    [Fact]
    public async Task Restart_keeps_interrupted_when_startup_recovery_already_cancelled_the_run()
    {
        // Startup recovery cancels a run with no in-memory execution before batch
        // recovery reads it. The batch item must not keep that cancel text.
        var harness = Harness();
        var queued = harness.AddScenario("queued");
        var runId = Guid.NewGuid();
        harness.Runs.Status[runId] = AutomationRunStatus.Cancelled;
        harness.Runs.Errors[runId] = "Cancelled by user (no active execution in this process).";
        await harness.Batches.InsertAsync(new ReportBatchDocument
        {
            BatchId = Guid.NewGuid(),
            EhrSystem = "local",
            MaxConcurrency = 1,
            Status = ReportBatchStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new ReportBatchItemDocument
                {
                    Ordinal = 0,
                    ScenarioId = Guid.NewGuid(),
                    ScenarioName = "in flight",
                    Status = ReportBatchItemStatus.Running,
                    RunId = runId
                },
                new ReportBatchItemDocument
                {
                    Ordinal = 1,
                    ScenarioId = queued.Id,
                    ScenarioName = queued.Name,
                    Status = ReportBatchItemStatus.Queued
                }
            ]
        });

        await harness.Dispatcher.RecoverAsync(CancellationToken.None);
        var afterRecovery = (await harness.Batches.ListActiveAsync()).Single();
        afterRecovery.Items[0].Status.Should().Be(ReportBatchItemStatus.Cancelled);
        afterRecovery.Items[0].Error.Should().Be(ReportBatchDispatcher.InterruptedByRestart);
        afterRecovery.Items[1].Status.Should().Be(ReportBatchItemStatus.Queued);
        harness.Runs.Cancelled.Should().BeEmpty();

        await harness.Dispatcher.PumpAsync(CancellationToken.None);

        var stored = (await harness.Batches.ListActiveAsync()).Single();
        stored.Items[1].Status.Should().Be(ReportBatchItemStatus.Running);
        harness.Runs.Started.Should().ContainSingle();
        harness.Runs.Started[0].ScenarioId.Should().Be(queued.Id);
    }

    [Fact]
    public async Task Restart_keeps_a_run_that_already_finished()
    {
        var harness = Harness();
        var runId = Guid.NewGuid();
        harness.Runs.Status[runId] = AutomationRunStatus.Succeeded;
        var batchId = Guid.NewGuid();
        await harness.Batches.InsertAsync(new ReportBatchDocument
        {
            BatchId = batchId,
            EhrSystem = "local",
            MaxConcurrency = 2,
            Status = ReportBatchStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new ReportBatchItemDocument
                {
                    Ordinal = 0,
                    ScenarioId = Guid.NewGuid(),
                    Status = ReportBatchItemStatus.Running,
                    RunId = runId
                }
            ]
        });

        await harness.Dispatcher.RecoverAsync(CancellationToken.None);

        var stored = await harness.Batches.GetAsync(batchId);
        stored!.Items[0].Status.Should().Be(ReportBatchItemStatus.Succeeded);
        stored.Status.Should().Be(ReportBatchStatus.Completed);
        harness.Runs.Cancelled.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_save_after_start_cancels_that_run_and_the_next_pump_starts_one()
    {
        var harness = Harness();
        harness.Batches.FailNextReplace = true;
        var scenario = harness.AddScenario("one");

        var act = () => harness.Dispatcher.StartBatchAsync("local", [scenario.Id], null, CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        harness.Runs.Started.Should().ContainSingle();
        harness.Runs.Cancelled.Should().ContainSingle();
        var orphan = harness.Runs.Cancelled[0];
        harness.Runs.Status[orphan].Should().Be(AutomationRunStatus.Cancelled);

        var stored = (await harness.Batches.ListActiveAsync()).Should().ContainSingle().Subject;
        stored.Items.Should().ContainSingle();
        stored.Items[0].Status.Should().Be(ReportBatchItemStatus.Queued);
        stored.Items[0].RunId.Should().BeNull();

        await harness.Dispatcher.PumpAsync(CancellationToken.None);

        harness.Runs.Started.Should().HaveCount(2);
        var after = await harness.Batches.GetAsync(stored.BatchId);
        after!.Items.Should().ContainSingle();
        after.Items[0].Status.Should().Be(ReportBatchItemStatus.Running);
        after.Items[0].RunId.Should().NotBe(orphan);
        harness.Runs.Status.Count(pair => pair.Value == AutomationRunStatus.Running).Should().Be(1);
    }

    [Fact]
    public void The_batch_gateway_reads_run_status_without_the_run_logs()
    {
        var gateway = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/ReportBatches/AutomationRunManagerBatchGateway.cs"));
        gateway.Should().Contain("runs.GetRunForDisplayAsync(runId, cancellationToken)");
        gateway.Should().NotContain("runs.GetRunAsync(");
    }

    [Fact]
    public async Task An_unknown_ehr_or_a_missing_scenario_does_not_create_a_batch()
    {
        var harness = Harness();
        var id = harness.AddScenario("one").Id;

        var unknownEhr = () => harness.Dispatcher.StartBatchAsync("missing", [id], null, CancellationToken.None);
        await unknownEhr.Should().ThrowAsync<InvalidOperationException>();

        var missingScenario = () => harness.Dispatcher.StartBatchAsync("local", [Guid.NewGuid()], null, CancellationToken.None);
        await missingScenario.Should().ThrowAsync<InvalidOperationException>();

        (await harness.Batches.ListActiveAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task The_settings_api_reads_and_rejects_the_cap()
    {
        var settings = new MemorySettings();
        var controller = new ReportBatchSettingsApiController(Provider(settings));

        var read = await controller.Get(CancellationToken.None);
        read.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ReportBatchSettingsResponse>()
            .Which.MaxConcurrency.Should().Be(2);

        var saved = await controller.Put(new ReportBatchSettingsRequest { MaxConcurrency = 6 }, CancellationToken.None);
        saved.Should().BeOfType<AcceptedResult>()
            .Which.Value.Should().BeOfType<ReportBatchSettingsResponse>()
            .Which.MaxConcurrency.Should().Be(6);
        (await settings.GetMaxConcurrencyAsync()).Should().Be(6);

        var rejected = await controller.Put(new ReportBatchSettingsRequest { MaxConcurrency = 0 }, CancellationToken.None);
        rejected.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
        (await settings.GetMaxConcurrencyAsync()).Should().Be(6);
    }

    [Fact]
    public async Task The_batch_api_starts_and_returns_status()
    {
        var harness = Harness();
        var controller = new ReportBatchesApiController(Provider(harness.Dispatcher));
        var scenario = harness.AddScenario("one");

        var started = await controller.Start(
            new StartReportBatchRequest { EhrSystem = "local", ScenarioIds = [scenario.Id] },
            CancellationToken.None);
        var body = started.Should().BeOfType<AcceptedAtActionResult>().Subject;
        body.StatusCode.Should().Be(202);
        var created = body.Value.Should().BeOfType<ReportBatchResponse>().Subject;
        created.Status.Should().Be(ReportBatchStatus.Running);
        created.Items.Should().ContainSingle();
        created.Items[0].RunId.Should().NotBeNull();

        var status = await controller.Get(created.BatchId, CancellationToken.None);
        status.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ReportBatchResponse>()
            .Which.BatchId.Should().Be(created.BatchId);

        var missing = await controller.Get(Guid.NewGuid(), CancellationToken.None);
        missing.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task The_settings_page_shows_the_default_and_saves_a_new_cap()
    {
        var settings = new MemorySettings();
        var controller = Page(settings);

        var page = await controller.Index(CancellationToken.None);
        page.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeOfType<ReportBatchSettingsPage>()
            .Which.MaxConcurrency.Should().Be(2);

        var saved = await controller.Save(4, CancellationToken.None);
        saved.Should().BeOfType<RedirectToActionResult>();
        (await settings.GetMaxConcurrencyAsync()).Should().Be(4);

        var rejected = await controller.Save(12, CancellationToken.None);
        rejected.Should().BeOfType<RedirectToActionResult>();
        (await settings.GetMaxConcurrencyAsync()).Should().Be(4);
    }

    [Fact]
    public void The_batches_page_is_on_the_automation_nav_and_recovery_is_registered_after_run_recovery()
    {
        var nav = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_Nav.cshtml"));
        nav.Should().Contain("asp-controller=\"ReportBatchSettings\"");

        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ReportBatchSettings/Index.cshtml"));
        view.Should().Contain("name=\"maxConcurrency\"");
        view.Should().Contain("Save settings");
        view.Should().Contain("asp-action=\"History\"");

        var engine = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/LinkAutomationEngine.cs"));
        var runRecovery = engine.IndexOf("AddHostedService<ScenarioRunStartupRecoveryService>", StringComparison.Ordinal);
        var apiHealthRecovery = engine.IndexOf("AddHostedService<ApiHealthStartupRecoveryService>", StringComparison.Ordinal);
        var batches = engine.IndexOf("GetRequiredService<ReportBatchDispatcher>()", StringComparison.Ordinal);
        runRecovery.Should().BeGreaterThanOrEqualTo(0);
        batches.Should().BeGreaterThan(apiHealthRecovery);
        apiHealthRecovery.Should().BeGreaterThan(runRecovery);
    }

    [Fact]
    public void Stored_batch_statuses_use_the_tracking_words()
    {
        ReportBatchDisplay.ItemLabel(ReportBatchItemStatus.Queued).Should().Be("Pending");
        ReportBatchDisplay.ItemLabel(ReportBatchItemStatus.Running).Should().Be("In Progress");
        ReportBatchDisplay.ItemLabel(ReportBatchItemStatus.Succeeded).Should().Be("Completed");
        ReportBatchDisplay.ItemLabel(ReportBatchItemStatus.Failed).Should().Be("Failed");
        ReportBatchDisplay.ItemLabel(ReportBatchItemStatus.Cancelled).Should().Be("Cancelled");
        ReportBatchDisplay.ItemLabel(" ").Should().Be("Unknown");
        ReportBatchDisplay.ItemLabel("Collecting").Should().Be("Collecting");
        ReportBatchDisplay.BatchLabel(ReportBatchStatus.Queued).Should().Be("Pending");
        ReportBatchDisplay.BatchLabel(ReportBatchStatus.Running).Should().Be("In Progress");
        ReportBatchDisplay.BatchLabel(ReportBatchStatus.Completed).Should().Be("Completed");
        ReportBatchDisplay.BatchLabel(ReportBatchStatus.Cancelled).Should().Be("Cancelled");
    }

    [Fact]
    public async Task The_tracking_page_shows_each_run_under_its_ehr()
    {
        var batches = new MemoryBatches();
        var failedRun = Guid.NewGuid();
        var completedRun = Guid.NewGuid();
        var batch = new ReportBatchDocument
        {
            BatchId = Guid.NewGuid(),
            EhrSystem = "local",
            Status = ReportBatchStatus.Running,
            MaxConcurrency = 2,
            CreatedAt = DateTimeOffset.Parse("2026-11-12T15:00:00Z"),
            Items =
            [
                new ReportBatchItemDocument
                {
                    Ordinal = 2,
                    ScenarioId = Guid.NewGuid(),
                    ScenarioName = "waiting",
                    Status = ReportBatchItemStatus.Queued
                },
                new ReportBatchItemDocument
                {
                    Ordinal = 0,
                    ScenarioId = Guid.NewGuid(),
                    ScenarioName = "alpha",
                    Status = ReportBatchItemStatus.Failed,
                    Error = "boom",
                    RunId = failedRun
                },
                new ReportBatchItemDocument
                {
                    Ordinal = 1,
                    ScenarioId = Guid.NewGuid(),
                    ScenarioName = "metrics",
                    IsMetricsRun = true,
                    Status = ReportBatchItemStatus.Succeeded,
                    RunId = completedRun
                }
            ]
        };
        await batches.InsertAsync(batch);
        var controller = Tracking(batches);

        var page = await controller.Detail(batch.BatchId, CancellationToken.None);
        var shown = page.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeOfType<ReportBatchTrackingPage>().Subject;
        shown.Found.Should().BeTrue();
        shown.Batch!.Status.Should().Be("In Progress");
        shown.Batch.CanCancel.Should().BeTrue();
        shown.Batch.Groups.Should().ContainSingle().Which.EhrSystem.Should().Be("local");
        var rows = shown.Batch.Groups[0].Items;
        rows.Select(row => row.ScenarioName).Should().Equal("alpha", "metrics", "waiting");
        rows[0].Status.Should().Be("Failed");
        rows[0].Error.Should().Be("boom");
        rows[0].EhrSystem.Should().Be("local");
        rows[0].RunHref.Should().Be("/Automation/Runs/" + failedRun.ToString("D"));
        rows[1].Status.Should().Be("Completed");
        rows[1].IsMetricsRun.Should().BeTrue();
        rows[1].RunHref.Should().Be("/Automation/Runs/" + completedRun.ToString("D"));
        rows[2].Status.Should().Be("Pending");
        rows[2].RunHref.Should().BeNull();
        shown.Batch.Pending.Should().Be(1);
        shown.Batch.Completed.Should().Be(1);
        shown.Batch.Failed.Should().Be(1);

        var status = await controller.Status(batch.BatchId, CancellationToken.None);
        status.Should().BeOfType<JsonResult>()
            .Which.Value.Should().BeOfType<ReportBatchTrackingPage>()
            .Which.Batch!.Failed.Should().Be(1);

        var missing = await controller.Detail(Guid.NewGuid(), CancellationToken.None);
        missing.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeOfType<ReportBatchTrackingPage>()
            .Which.Found.Should().BeFalse();
        controller.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var missingStatus = await controller.Status(Guid.NewGuid(), CancellationToken.None);
        missingStatus.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Cancel_on_the_tracking_page_stops_an_open_batch()
    {
        var harness = Harness();
        var started = await harness.Dispatcher.StartBatchAsync("local", Ids(harness, 2), 1, CancellationToken.None);
        var controller = Tracking(harness.Batches, harness.Dispatcher);

        var cancelled = await controller.Cancel(started.BatchId, CancellationToken.None);
        cancelled.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Detail");

        var after = await controller.Status(started.BatchId, CancellationToken.None);
        var body = after.Should().BeOfType<JsonResult>()
            .Which.Value.Should().BeOfType<ReportBatchTrackingPage>().Subject;
        body.Batch!.Status.Should().Be("Cancelled");
        body.Batch.CanCancel.Should().BeFalse();
        body.Batch.Groups.Should().ContainSingle().Which.EhrSystem.Should().Be("local");
        body.Batch.Groups[0].Items.Should().OnlyContain(row => row.Status == "Cancelled");
        body.Batch.Cancelled.Should().Be(2);

        var missing = await controller.Cancel(Guid.NewGuid(), CancellationToken.None);
        missing.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task History_groups_batches_by_ehr_newest_first()
    {
        var batches = new MemoryBatches();
        var older = new ReportBatchDocument
        {
            BatchId = Guid.NewGuid(),
            EhrSystem = "local",
            Status = ReportBatchStatus.Completed,
            CreatedAt = DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
            Items = [new ReportBatchItemDocument()]
        };
        var newer = new ReportBatchDocument
        {
            BatchId = Guid.NewGuid(),
            EhrSystem = "Local",
            Status = ReportBatchStatus.Running,
            CreatedAt = DateTimeOffset.Parse("2026-11-10T00:00:00Z"),
            Items = [new ReportBatchItemDocument(), new ReportBatchItemDocument()]
        };
        var launchId = Guid.NewGuid();
        var other = new ReportBatchDocument
        {
            BatchId = Guid.NewGuid(),
            EhrSystem = "clinic",
            LaunchId = launchId,
            Status = ReportBatchStatus.Queued,
            CreatedAt = DateTimeOffset.Parse("2026-11-11T00:00:00Z"),
            Items = [new ReportBatchItemDocument()]
        };
        await batches.InsertAsync(older);
        await batches.InsertAsync(newer);
        await batches.InsertAsync(other);

        var limited = await batches.ListRecentAsync(2, CancellationToken.None);
        limited.Select(batch => batch.BatchId).Should().Equal(other.BatchId, newer.BatchId);

        var page = await Tracking(batches).History(CancellationToken.None);
        var model = page.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeOfType<ReportBatchHistoryPage>().Subject;
        model.Ready.Should().BeTrue();
        model.Groups.Select(group => group.EhrSystem).Should().Equal("clinic", "Local");
        model.Groups[0].Batches.Should().ContainSingle().Which.Status.Should().Be("Pending");
        model.Groups[0].Batches[0].Href.Should().Be("/Automation/ReportBatches/" + other.BatchId.ToString("D"));
        model.Groups[0].Batches[0].LaunchHref.Should().Be("/Automation/ReportBatches/Launches/" + launchId.ToString("D"));
        model.Groups[1].Batches.Select(batch => batch.BatchId).Should().Equal(newer.BatchId, older.BatchId);
        model.Groups[1].Batches[0].LaunchHref.Should().BeNull();
        model.Groups[1].Batches[0].Status.Should().Be("In Progress");
        model.Groups[1].Batches[0].ItemCount.Should().Be(2);

        var unavailable = await Tracking(null).History(CancellationToken.None);
        unavailable.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeOfType<ReportBatchHistoryPage>()
            .Which.Ready.Should().BeFalse();
    }

    [Fact]
    public async Task A_launch_keeps_an_ampersand_in_the_EHR_name()
    {
        var harness = Harness();
        var scenario = harness.AddScenario("alpha");

        var launch = await harness.Dispatcher.StartLaunchAsync(
            [new ReportBatchLaunchSlice("R&D", [scenario.Id], 1)],
            CancellationToken.None);

        launch.Batches.Should().ContainSingle().Which.EhrSystem.Should().Be("R&D");
    }

    [Fact]
    public async Task A_launch_starts_one_batch_per_EHR_without_waiting()
    {
        var harness = Harness();
        var local = harness.AddScenario("local-scenario");
        var clinic = harness.AddScenario("clinic-scenario");

        var launch = await harness.Dispatcher.StartLaunchAsync(
            [
                new ReportBatchLaunchSlice("local", [local.Id, local.Id], 1),
                new ReportBatchLaunchSlice("clinic", [clinic.Id], 1)
            ],
            CancellationToken.None);

        launch.Batches.Should().HaveCount(2);
        launch.Batches.Should().OnlyContain(batch => batch.LaunchId == launch.LaunchId);
        launch.Batches.Single(batch => batch.EhrSystem == "local").Items.Select(item => item.ScenarioId)
            .Should().Equal(local.Id, local.Id);
        launch.Batches.Single(batch => batch.EhrSystem == "clinic").Items.Should().ContainSingle()
            .Which.ScenarioId.Should().Be(clinic.Id);
        harness.Runs.Started.Select(request => request.EhrSystem).Should().Equal("local", "clinic");
        launch.Batches.Should().OnlyContain(batch =>
            batch.Items.Any(item => item.Status == ReportBatchItemStatus.Running));
    }

    [Fact]
    public async Task A_second_batch_that_fails_to_create_cancels_the_first()
    {
        var harness = Harness();
        var scenario = harness.AddScenario("alpha");

        var act = () => harness.Dispatcher.StartLaunchAsync(
            [
                new ReportBatchLaunchSlice("local", [scenario.Id], 1),
                new ReportBatchLaunchSlice("clinic", [Guid.NewGuid()], 1)
            ],
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*was not found*");
        (await harness.Batches.ListActiveAsync()).Should().BeEmpty();
        var stored = await harness.Batches.ListRecentAsync(10, CancellationToken.None);
        stored.Should().ContainSingle();
        stored[0].EhrSystem.Should().Be("local");
        stored[0].CancelRequested.Should().BeTrue();
        stored[0].Status.Should().Be(ReportBatchStatus.Cancelled);
    }

    [Fact]
    public async Task A_failed_insert_cancels_batches_already_created()
    {
        var harness = Harness();
        harness.Batches.FailInsertNumber = 2;
        var first = harness.AddScenario("alpha");
        var second = harness.AddScenario("beta");

        var act = () => harness.Dispatcher.StartLaunchAsync(
            [
                new ReportBatchLaunchSlice("local", [first.Id], 1),
                new ReportBatchLaunchSlice("clinic", [second.Id], 1)
            ],
            CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        (await harness.Batches.ListActiveAsync()).Should().BeEmpty();
        var stored = await harness.Batches.ListRecentAsync(10, CancellationToken.None);
        stored.Should().ContainSingle().Which.Status.Should().Be(ReportBatchStatus.Cancelled);
    }

    [Fact]
    public async Task A_batch_saved_before_dispatch_fails_is_cancelled_with_the_launch()
    {
        var harness = Harness();
        harness.Batches.FailNextReplace = true;
        var scenario = harness.AddScenario("alpha");

        var act = () => harness.Dispatcher.StartLaunchAsync(
            [new ReportBatchLaunchSlice("local", [scenario.Id], 1)],
            CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        (await harness.Batches.ListActiveAsync()).Should().BeEmpty();
        var stored = await harness.Batches.ListRecentAsync(10, CancellationToken.None);
        stored.Should().ContainSingle();
        stored[0].CancelRequested.Should().BeTrue();
        stored[0].Status.Should().Be(ReportBatchStatus.Cancelled);
    }

    [Fact]
    public async Task The_same_EHR_cannot_be_launched_twice()
    {
        var harness = Harness();
        var scenario = harness.AddScenario("alpha");

        var act = () => harness.Dispatcher.StartLaunchAsync(
            [
                new ReportBatchLaunchSlice("local", [scenario.Id], 1),
                new ReportBatchLaunchSlice("local", [scenario.Id], 1)
            ],
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*more than once*");
        (await harness.Batches.ListRecentAsync(10, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelling_a_launch_cancels_every_open_batch()
    {
        var harness = Harness();
        var local = harness.AddScenario("local-scenario");
        var clinic = harness.AddScenario("clinic-scenario");
        var launch = await harness.Dispatcher.StartLaunchAsync(
            [
                new ReportBatchLaunchSlice("local", [local.Id], 1),
                new ReportBatchLaunchSlice("clinic", [clinic.Id], 1)
            ],
            CancellationToken.None);

        await harness.Dispatcher.CancelLaunchAsync(launch.LaunchId, CancellationToken.None);

        var stored = await harness.Batches.ListByLaunchAsync(launch.LaunchId, CancellationToken.None);
        stored.Should().HaveCount(2);
        stored.Should().OnlyContain(batch => batch.Status == ReportBatchStatus.Cancelled);
    }

    [Fact]
    public void The_tracking_page_polls_the_shell_status_action()
    {
        var detail = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ReportBatchTracking/Detail.cshtml"));
        detail.Should().Contain("Scenario");
        detail.Should().Contain(">EHR<");
        detail.Should().Contain("Cancel batch");
        detail.Should().Contain("data-ordinal");

        var history = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/ReportBatchTracking/History.cshtml"));
        history.Should().Contain("No batches yet.");
        history.Should().Contain("batch.LaunchHref");

        detail.Should().Contain("<span data-item-error>");
        detail.Should().Contain("batch.LaunchHref");

        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/automation-batch.js"));
        script.Should().Contain("2000");
        script.Should().NotContain("signalR");
        script.Should().NotContain("/api/report-batches");
        script.Should().Contain("if (response.status === 404)");
        script.Should().Contain("if (!response.ok) return null;");
        script.Should().Contain("if (error.textContent !== next) error.textContent = next;");
    }

    private const string ParallelRunSafetyMessage =
        "A Metrics run is already in progress. Metrics runs share Prometheus data, so this one cannot start until that run finishes.";

    private static BatchHarness Harness(MemorySettings? settings = null)
        => new(settings ?? new MemorySettings());

    private static List<Guid> Ids(BatchHarness harness, int count)
        => Enumerable.Range(0, count).Select(index => harness.AddScenario("scenario-" + index).Id).ToList();

    private static ServiceProvider Provider(object service)
    {
        var services = new ServiceCollection();
        if (service is IReportBatchSettingsStore settings)
            services.AddSingleton<IReportBatchSettingsStore>(settings);
        if (service is ReportBatchDispatcher dispatcher)
            services.AddSingleton(dispatcher);
        return services.BuildServiceProvider();
    }

    private static ReportBatchTrackingController Tracking(MemoryBatches? batches, ReportBatchDispatcher? dispatcher = null)
    {
        var services = new ServiceCollection();
        if (batches is not null)
            services.AddSingleton<IReportBatchStore>(batches);
        if (dispatcher is not null)
            services.AddSingleton(dispatcher);
        return new ReportBatchTrackingController(services.BuildServiceProvider())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new UnusedTempData())
        };
    }

    private static ReportBatchSettingsController Page(MemorySettings settings)
    {
        var controller = new ReportBatchSettingsController(Provider(settings))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new UnusedTempData())
        };
        return controller;
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class BatchHarness
    {
        public BatchHarness(MemorySettings settings)
        {
            Settings = settings;
            Batches = new MemoryBatches();
            Scenarios = new MemoryScenarios();
            Runs = new FakeRuns();
            Dispatcher = new ReportBatchDispatcher(
                Batches,
                Settings,
                Scenarios,
                new FakeEhr(),
                Runs,
                TimeProvider.System,
                NullLogger<ReportBatchDispatcher>.Instance);
        }

        public MemorySettings Settings { get; }
        public MemoryBatches Batches { get; }
        public MemoryScenarios Scenarios { get; }
        public FakeRuns Runs { get; }
        public ReportBatchDispatcher Dispatcher { get; }

        public TestScenarioDefinition AddScenario(string name, bool metrics = false)
        {
            var scenario = new TestScenarioDefinition { Name = name, IsMetricsRun = metrics };
            Scenarios.Items[scenario.Id] = scenario;
            return scenario;
        }
    }

    private sealed class FakeEhr : IReportBatchEhrLookup
    {
        public string? CanonicalName(string? requestedName)
        {
            var trimmed = requestedName?.Trim();
            if (string.Equals(trimmed, "local", StringComparison.OrdinalIgnoreCase))
                return "local";
            if (string.Equals(trimmed, "clinic", StringComparison.OrdinalIgnoreCase))
                return "clinic";
            if (string.Equals(trimmed, "R&D", StringComparison.Ordinal))
                return "R&D";
            return null;
        }
    }

    private sealed class MemorySettings : IReportBatchSettingsStore
    {
        private int? _saved;

        public Task<int> GetMaxConcurrencyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_saved is int value && ReportBatchConcurrencyPolicy.Validate(value) is null
                ? value
                : ReportBatchConcurrencyPolicy.DefaultMaxConcurrency);

        public Task SaveMaxConcurrencyAsync(int maxConcurrency, CancellationToken cancellationToken = default)
        {
            var error = ReportBatchConcurrencyPolicy.Validate(maxConcurrency);
            if (error != null)
                throw new InvalidOperationException(error);

            _saved = maxConcurrency;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryBatches : IReportBatchStore
    {
        private readonly Dictionary<Guid, ReportBatchDocument> _batches = [];

        public bool FailNextReplace { get; set; }

        public int FailInsertNumber { get; set; }

        private int _inserts;

        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InsertAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default)
        {
            _inserts++;
            if (FailInsertNumber > 0 && _inserts == FailInsertNumber)
                throw new IOException("insert failed");

            _batches[batch.BatchId] = Clone(batch);
            return Task.CompletedTask;
        }

        public Task<ReportBatchDocument?> GetAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_batches.TryGetValue(batchId, out var batch) ? Clone(batch) : null);

        public Task ReplaceAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default)
        {
            if (FailNextReplace)
            {
                FailNextReplace = false;
                throw new IOException("replace failed");
            }

            if (!_batches.ContainsKey(batch.BatchId))
                throw new InvalidOperationException($"Batch {batch.BatchId} was not inserted.");

            _batches[batch.BatchId] = Clone(batch);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReportBatchDocument>> ListActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReportBatchDocument>>(_batches.Values
                .Where(batch => ReportBatchStatus.IsOpen(batch.Status))
                .Select(Clone)
                .ToList());

        public Task<IReadOnlyList<ReportBatchDocument>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            if (limit < 1)
                return Task.FromResult<IReadOnlyList<ReportBatchDocument>>([]);

            IReadOnlyList<ReportBatchDocument> rows = _batches.Values
                .OrderByDescending(batch => batch.CreatedAt)
                .ThenBy(batch => batch.BatchId)
                .Take(limit)
                .Select(Clone)
                .ToList();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<ReportBatchDocument>> ListByLaunchAsync(
            Guid launchId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReportBatchDocument>>(_batches.Values
                .Where(batch => batch.LaunchId == launchId)
                .Select(Clone)
                .ToList());

        private static ReportBatchDocument Clone(ReportBatchDocument batch)
            => new()
            {
                BatchId = batch.BatchId,
                EhrSystem = batch.EhrSystem,
                LaunchId = batch.LaunchId,
                MaxConcurrency = batch.MaxConcurrency,
                Status = batch.Status,
                CancelRequested = batch.CancelRequested,
                CreatedAt = batch.CreatedAt,
                StartedAt = batch.StartedAt,
                FinishedAt = batch.FinishedAt,
                Items = batch.Items.Select(item => new ReportBatchItemDocument
                {
                    Ordinal = item.Ordinal,
                    ScenarioId = item.ScenarioId,
                    ScenarioName = item.ScenarioName,
                    IsMetricsRun = item.IsMetricsRun,
                    RunId = item.RunId,
                    Status = item.Status,
                    Error = item.Error
                }).ToList()
            };
    }

    private sealed class MemoryScenarios : IScenarioStore
    {
        public Dictionary<Guid, TestScenarioDefinition> Items { get; } = [];

        public Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(Items.Values.ToList());

        public Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.GetValueOrDefault(id));

        public Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default)
        {
            Items[scenario.Id] = scenario;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Items.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRuns : IReportBatchRunGateway
    {
        public List<StartScenarioRequest> Started { get; } = [];
        public List<Guid> Cancelled { get; } = [];
        public Dictionary<Guid, AutomationRunStatus> Status { get; } = [];
        public Dictionary<Guid, string?> Errors { get; } = [];
        public HashSet<Guid> FailScenarioIds { get; } = [];
        public bool ThrowMetricsRefusal { get; set; }

        public Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
        {
            if (request.ScenarioId is Guid scenarioId && FailScenarioIds.Contains(scenarioId))
                throw new InvalidOperationException("start failed");

            if (ThrowMetricsRefusal && request.IsMetricsRun)
                throw new InvalidOperationException(ParallelRunSafetyMessage);

            var runId = Guid.NewGuid();
            Started.Add(request);
            Status[runId] = AutomationRunStatus.Running;
            return Task.FromResult(runId);
        }

        public Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            Cancelled.Add(runId);
            if (!Status.ContainsKey(runId))
                return Task.FromResult(false);

            Status[runId] = AutomationRunStatus.Cancelled;
            return Task.FromResult(true);
        }

        public Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            if (!Status.TryGetValue(runId, out var status))
                return Task.FromResult<AutomationRunSummary?>(null);

            return Task.FromResult<AutomationRunSummary?>(new AutomationRunSummary
            {
                RunId = runId,
                Status = status,
                Error = Errors.TryGetValue(runId, out var error)
                    ? error
                    : status switch
                    {
                        AutomationRunStatus.Failed => "run failed",
                        AutomationRunStatus.Cancelled => "Cancelled by user.",
                        _ => null
                    }
            });
        }
    }

    private sealed class UnusedTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
