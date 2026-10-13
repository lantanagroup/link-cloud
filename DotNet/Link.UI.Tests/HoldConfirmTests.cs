using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class HoldConfirmTests
{
    private static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "Link.UI", "Views")))
            dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir!, "Link.UI");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    [Fact]
    public void Spec_serializes_action_details_risks_and_confirm()
    {
        var json = Hold.Of("Produce a message", "Produce one new message to T.", new[] { Hold.Pair("Topic", "T") }, new[] { new[] { "reason", "Reason" } }, new[] { "Cannot be recalled." }, "T", "Hold to produce");
        json.Should().Contain("\"title\":\"Produce a message\"").And.Contain("\"details\":[[\"Topic\",\"T\"]]").And.Contain("\"fields\":[[\"reason\",\"Reason\"]]").And.Contain("\"risks\":[\"Cannot be recalled.\"]").And.Contain("\"confirm\":\"T\"").And.Contain("\"label\":\"Hold to produce\"");
        Hold.DurationMs.Should().Be(1000);
    }

    [Fact]
    public void Modal_is_shared_from_the_layout_and_says_are_you_sure()
    {
        var layout = Read("Views", "Shared", "_Layout.cshtml");
        layout.Should().Contain("<partial name=\"_HoldConfirmModal\" />").And.Contain("~/js/hold-confirm.js");
        var modal = Read("Views", "Shared", "_HoldConfirmModal.cshtml");
        modal.Should().Contain("Are you sure?").And.Contain("id=\"lu-hold-risks\"").And.Contain("role=\"progressbar\"").And.Contain("aria-live=\"polite\"").And.Contain("data-hold-ms=\"@Hold.DurationMs\"");
    }

    [Fact]
    public void Hold_script_handles_pointer_keyboard_release_and_escape()
    {
        var js = Read("wwwroot", "js", "hold-confirm.js");
        foreach (var token in new[] { "pointerdown", "pointerup", "pointerleave", "pointercancel", "keydown", "keyup", "ev.repeat", "requestAnimationFrame", "aria-valuenow", "data-hold-target", "requestSubmit", "stopPropagation" })
            js.Should().Contain(token);
    }

    [Fact]
    public void Kafka_console_has_no_type_the_name_fields_and_risky_actions_hold()
    {
        foreach (var file in new[] { "_ProduceMessage.cshtml", "_Migrate.cshtml", "_ReplicationFactor.cshtml", "_Brokers.cshtml" })
        {
            var view = Read("Views", "Operations", file);
            view.Should().NotContain("Type the topic name", file);
            view.Should().NotContain("Type the backup topic name", file);
            view.Should().Contain("data-hold=\"@Hold.Of(", file);
        }
        var migrate = Read("Views", "Operations", "_Migrate.cshtml");
        migrate.Should().Contain("id=\"plan-submit\" type=\"submit\" data-hold=").And.Contain("formaction=\"/Operations/Kafka/migrations\" data-hold=").And.Contain("formaction=\"/Operations/Kafka/family\" data-hold=");
        System.Text.RegularExpressions.Regex.Matches(migrate, "data-hold=\"@Hold.Of\\(").Count.Should().BeGreaterThanOrEqualTo(8);
        Read("Views", "Operations", "_Brokers.cshtml").Should().Contain("brokers/decommission\" data-hold=").And.Contain("brokers/rebalance\" data-hold=").And.Contain("type=\"submit\" data-hold=\"@Hold.Of(\"Add a broker\"");
        Read("Views", "Operations", "_ReplicationFactor.cshtml").Should().Contain("replication-factor\" data-hold=");
        Read("wwwroot", "js", "kafka-ops.js").Should().NotContain("plan-confirm").And.NotContain("produce-confirm");
    }

    [Fact]
    public void Approve_and_execute_only_show_while_they_can_run()
    {
        var view = Read("Views", "Operations", "_ChangeRequest.cshtml");
        view.Should().Contain("@if (KafkaChangeActions.CanApprove(request))").And.Contain("@if (KafkaChangeActions.CanExecute(request))").And.Contain("data-hold=\"@KafkaChangeActions.ExecuteHold(request)\"");
    }
}
