using Link.UI.Models;

namespace Link.UI.Services;

public sealed class RubricActionPlan
{
    public string Action { get; init; } = "";
    public string Heading { get; set; } = "";
    public string ConfirmLabel { get; init; } = "";
    public List<string> Consequences { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public string? Blocked { get; set; }
    public bool NeedsDryRun { get; set; }
    /// <summary>
    /// Admin.UI asks for the rubric id to be typed when retiring a compared rubric.
    /// Link.UI states the same risk on the hold-to-confirm button and does not ask for typed text.
    /// </summary>
    public string? TypeToConfirm { get; set; }
}

public static class RubricLifecycle
{
    public static IReadOnlyList<string> ActionsFor(string? status) => status switch
    {
        "DRAFT" => ["publish", "delete"],
        "PUBLISHED" => ["retire"],
        _ => []
    };

    public static string ActionName(string action) => action switch
    {
        "publish" => "Publish",
        "retire" => "Retire",
        "delete" => "Delete",
        _ => action
    };

    public static string? ActiveAfterRetiring(RubricDetail? rubric, string semver) =>
        (rubric?.Versions ?? [])
            .Where(version => version.Status == "PUBLISHED" && version.Semver != semver)
            .OrderByDescending(version => version.PublishedAt?.ToString("o") ?? "", StringComparer.Ordinal)
            .Select(version => version.Semver)
            .FirstOrDefault();

    public static RubricActionPlan Plan(
        string action,
        string rubricId,
        string semver,
        string? status,
        DateTimeOffset? dryRunCompletedAt,
        RubricDetail? rubric,
        RubricSettings? settings,
        int? enabledCheckCount)
    {
        var plan = new RubricActionPlan
        {
            Action = action,
            Heading = ActionName(action) + " version " + semver + "?",
            ConfirmLabel = ActionName(action)
        };
        var use = RubricText.PipelineUse(settings, rubricId);
        var active = rubric?.LatestPublishedSemver;

        switch (action)
        {
            case "publish":
                if (status != "DRAFT")
                    return Block(plan, "Only a draft can be published.");
                plan.Consequences.Add(semver + " becomes the active version of this rubric: the one validation uses.");
                if (!string.IsNullOrEmpty(active))
                    plan.Consequences.Add(active + " stays published but is no longer used.");
                plan.Consequences.Add("A published version can never be edited. To change it later, create a new version from it.");
                if (use == "decides")
                    plan.Warnings.Add("The reporting pipeline validates patient data against this rubric. Once published, this version decides whether that data passes.");
                else if (use == "compared")
                    plan.Warnings.Add("The reporting pipeline runs this rubric beside the validator that decides. Once published, this version is the one compared.");
                if (enabledCheckCount == 0)
                    plan.Warnings.Add("None of its checks is enabled, so it will find nothing.");
                if (dryRunCompletedAt is null)
                {
                    if (settings?.DryRunRequiredForPublish == true)
                        return Block(plan, "This environment requires a dry run before publishing, and this draft has not had one since it was last saved.", needsDryRun: true);
                    if (settings is not null)
                        plan.Warnings.Add("This draft has not been dry-run since it was last saved. It can still be published.");
                    else
                        plan.Warnings.Add("This draft has not been dry-run since it was last saved. If this environment requires a dry run, publishing will be refused.");
                }

                return plan;

            case "retire":
                if (status != "PUBLISHED")
                    return Block(plan, status == "DRAFT" ? "A draft is deleted, not retired." : "This version is already retired.");
                var next = ActiveAfterRetiring(rubric, semver);
                var noneLeft = rubric is not null && next is null;
                if (use == "decides" && noneLeft)
                    return Block(plan, "The reporting pipeline validates patient data against this rubric, and this is its only published version. Publish another version before retiring this one.");
                if (rubric is null)
                    plan.Consequences.Add("Validation stops using this version.");
                else if (semver != active)
                    plan.Consequences.Add(!string.IsNullOrEmpty(active) ? active + " stays the active version." : "Validation stops using this version.");
                else if (next is not null)
                    plan.Consequences.Add(next + " becomes the active version again.");
                else
                    plan.Consequences.Add("No published version will remain, so validation against this rubric has nothing to run.");
                plan.Consequences.Add("A retired version cannot be published again. To bring it back, create a new version from it.");
                if (use == "decides" && rubric is null)
                    plan.Warnings.Add("The reporting pipeline validates patient data against this rubric. If this is its only published version, retiring it will be refused.");
                else if (use == "compared" && (noneLeft || rubric is null))
                {
                    plan.Warnings.Add(noneLeft
                        ? "The reporting pipeline compares this rubric with the validator that decides. With no published version, that comparison has nothing to run."
                        : "The reporting pipeline compares this rubric with the validator that decides. If no other version is published, that comparison has nothing to run.");
                    plan.TypeToConfirm = rubricId;
                }

                return plan;

            case "delete":
                plan.Heading = "Delete draft " + semver + "?";
                if (status != "DRAFT")
                    return Block(plan, "Only a draft can be deleted. A published version is retired.");
                plan.Consequences.Add("The draft and its checks are removed. This cannot be undone.");
                if (rubric is not null && (rubric.Versions?.Count ?? 0) <= 1)
                    plan.Consequences.Add("It is the rubric's only version, so the rubric is removed with it, history included.");
                else
                    plan.Consequences.Add("Its version number can be used again, and the deletion is recorded in the rubric's history.");
                return plan;

            default:
                return Block(plan, "That action is not available.");
        }
    }

    public static bool DeletingRemovesRubric(RubricDetail? rubric) =>
        rubric is not null && (rubric.Versions?.Count ?? 0) <= 1;

    public static string? CompareWith(RubricDetail? rubric, string? semver)
    {
        var versions = rubric?.Versions ?? [];
        var active = rubric?.LatestPublishedSemver;
        if (!string.IsNullOrEmpty(active) && active != semver)
            return active;
        var index = versions.FindIndex(version => version.Semver == semver);
        if (index >= 0 && index + 1 < versions.Count)
            return versions[index + 1].Semver;
        return versions.FirstOrDefault(version => version.Semver != semver)?.Semver;
    }

    public static string HoldSpec(RubricActionPlan plan)
    {
        var risks = plan.Consequences.Concat(plan.Warnings).ToList();
        var label = "Hold to " + plan.ConfirmLabel.ToLowerInvariant();
        return Hold.Of(plan.Heading, plan.Heading, [], [], risks, "", label);
    }

    private static RubricActionPlan Block(RubricActionPlan plan, string reason, bool needsDryRun = false)
    {
        plan.Blocked = reason;
        plan.NeedsDryRun = needsDryRun;
        return plan;
    }
}
