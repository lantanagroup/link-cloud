using System.Text.Json;

namespace Link.UI.Services;

/// <summary>
/// What the shared hold-to-confirm modal (_HoldConfirmModal + hold-confirm.js) shows before a risky submit:
/// the action and its specifics, the risks, and "Are you sure?". Fields are read from the button's form when the modal opens.
/// Confirm is written into the form's hidden [data-hold-target] input once the hold completes, so the server-side check still runs.
/// </summary>
public sealed record HoldSpec(string Title, string Action, IReadOnlyList<string[]> Details, IReadOnlyList<string[]> Fields, IReadOnlyList<string> Risks, string Confirm = "", string Label = "Hold to confirm")
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Json() => JsonSerializer.Serialize(this, Options);
}

public static class Hold
{
    public const int DurationMs = 1000;

    public static string[] Pair(string label, object? value) => [label, value?.ToString() ?? ""];

    public static string Of(string title, string action, IEnumerable<string[]> details, IEnumerable<string[]> fields, IEnumerable<string> risks, string confirm = "", string label = "Hold to confirm") =>
        new HoldSpec(title, action, details.ToList(), fields.ToList(), risks.ToList(), confirm, label).Json();

    public static string Of(string title, string action, string risk, string label) =>
        new HoldSpec(title, action, [], [], [risk], "", label).Json();
}
