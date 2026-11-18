using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class RubricPortTests
{
    [Fact]
    public void Publish_is_blocked_until_a_required_dry_run_exists()
    {
        var settings = new RubricSettings { DryRunRequiredForPublish = true, PipelineRubricId = "piqi.core", PipelineEnabled = true };
        var blocked = RubricLifecycle.Plan("publish", "piqi.core", "1.0.0", "DRAFT", null, Rubric(), settings, 1);
        blocked.Blocked.Should().NotBeNullOrEmpty();
        blocked.NeedsDryRun.Should().BeTrue();

        var ready = RubricLifecycle.Plan("publish", "piqi.core", "1.0.0", "DRAFT", DateTimeOffset.UtcNow, Rubric(), settings, 0);
        ready.Blocked.Should().BeNull();
        ready.Warnings.Should().Contain(warning => warning.Contains("None of its checks"));
        ready.TypeToConfirm.Should().BeNull();
    }

    [Fact]
    public void Publish_of_a_version_that_is_not_a_draft_is_blocked()
    {
        var plan = RubricLifecycle.Plan("publish", "piqi.core", "1.0.0", "PUBLISHED", DateTimeOffset.UtcNow, Rubric(), null, 1);
        plan.Blocked.Should().Be("Only a draft can be published.");
    }

    [Fact]
    public void Retiring_the_only_deciding_version_is_blocked_and_a_compared_one_names_the_risk()
    {
        var decides = new RubricSettings { PipelineRubricId = "piqi.core", PipelineEnabled = true };
        var blocked = RubricLifecycle.Plan("retire", "piqi.core", "1.0.0", "PUBLISHED", null, Rubric(), decides, 1);
        blocked.Blocked.Should().Contain("only published version");

        var compared = new RubricSettings { PipelineRubricId = "piqi.core", PipelineShadowEnabled = true };
        var plan = RubricLifecycle.Plan("retire", "piqi.core", "1.0.0", "PUBLISHED", null, Rubric(), compared, 1);
        plan.Blocked.Should().BeNull();
        plan.TypeToConfirm.Should().Be("piqi.core");
        plan.Warnings.Should().Contain(warning => warning.Contains("compares this rubric"));

        var draft = RubricLifecycle.Plan("retire", "piqi.core", "1.0.0", "DRAFT", null, Rubric(), null, 1);
        draft.Blocked.Should().Be("A draft is deleted, not retired.");
    }

    [Fact]
    public void Only_a_draft_can_be_deleted()
    {
        var plan = RubricLifecycle.Plan("delete", "piqi.core", "1.0.0", "PUBLISHED", null, Rubric(), null, 1);
        plan.Blocked.Should().Contain("Only a draft");
        RubricLifecycle.DeletingRemovesRubric(Rubric()).Should().BeTrue();
    }

    [Fact]
    public void The_hold_for_a_plan_does_not_ask_for_typed_text()
    {
        var plan = RubricLifecycle.Plan("retire", "piqi.core", "1.0.0", "PUBLISHED", null, Rubric(), new RubricSettings { PipelineRubricId = "piqi.core", PipelineShadowEnabled = true }, 1);
        var json = RubricLifecycle.HoldSpec(plan);
        json.Should().Contain("\"confirm\":\"\"");
        json.Should().Contain("Hold to retire");
        plan.TypeToConfirm.Should().Be("piqi.core");
    }

    [Fact]
    public void Compare_ignores_id_and_semver_and_duplicate_check_ids()
    {
        var before = Definition("old.id", "1.0.0");
        before.Checks.Add(Check("same", "one"));
        before.Checks.Add(Check("same", "two"));
        var after = Definition("new.id", "2.0.0");
        after.Checks.Add(Check("same", "one"));

        var comparison = RubricCompare.CompareDefinitions(before, after);
        comparison.Identical.Should().BeTrue();
        comparison.UnchangedCount.Should().Be(1);
    }

    [Fact]
    public void Next_version_bumps_the_newest_and_issues_reject_an_empty_draft()
    {
        RubricDraft.NextVersion(["1.2.3", "1.4.0"], "patch").Should().Be("1.4.1");
        RubricDraft.NextVersion(["1.2.3", "1.4.0"], "minor").Should().Be("1.5.0");
        RubricDraft.NextVersion(["1.2.3", "1.4.0"], "major").Should().Be("2.0.0");
        RubricDraft.NextVersion([], "patch").Should().Be("1.0.0");

        var issues = RubricDraft.FindIssues(new RubricDefinition { Semver = "0.0.0", ScoringPolicy = new RubricScoringPolicy() }, null);
        issues.Should().Contain(issue => issue.Field == "id");
        issues.Should().Contain(issue => issue.Message.Contains("0.0.0"));
        issues.Should().Contain(issue => issue.Field == "checks");
    }

    [Fact]
    public void A_sample_must_be_one_JSON_FHIR_resource()
    {
        RubricSample.Read("").Error.Should().Contain("Provide a sample");
        RubricSample.Read("<Patient></Patient>").Error.Should().Contain("XML");
        RubricSample.Read("not json").Error.Should().Contain("not valid JSON");
        RubricSample.Read("[]").Error.Should().Contain("resourceType");
        RubricSample.Read("{\"resourceType\":\"Patient\"}").Description.Should().Be("Patient resource");
        RubricSample.Read("{\"resourceType\":\"Bundle\",\"entry\":[{}]}").Description.Should().Be("Bundle with 1 entry");
    }

    [Fact]
    public void Import_effect_names_what_saving_the_draft_would_do()
    {
        RubricCompare.ImportEffect(null).Should().BeNull();
        RubricCompare.ImportEffect(Review(exists: false, status: null)).Should().Be("new-rubric");
        RubricCompare.ImportEffect(Review(exists: true, status: null)).Should().Be("new-version");
        RubricCompare.ImportEffect(Review(exists: true, status: "DRAFT", checksum: "aaa", existing: "aaa")).Should().Be("unchanged-draft");
        RubricCompare.ImportEffect(Review(exists: true, status: "DRAFT", checksum: "bbb", existing: "aaa")).Should().Be("replace-draft");
        RubricCompare.ImportEffect(Review(exists: true, status: "PUBLISHED")).Should().Be("immutable");
        RubricCompare.EffectText("immutable").Should().Contain("cannot be replaced");
    }

    [Fact]
    public void Saving_a_moved_draft_creates_the_new_number_before_deleting_the_old_one()
    {
        var same = RubricEditorLogic.PlanSave("1.0.0", "abc", "1.0.0", overwrite: false);
        same.MustNotExist.Should().BeFalse();
        same.ExpectedChecksum.Should().Be("abc");
        same.Moving.Should().BeFalse();

        var moved = RubricEditorLogic.PlanSave("1.0.0", "abc", "1.1.0", overwrite: false);
        moved.MustNotExist.Should().BeTrue();
        moved.Moving.Should().BeTrue();
        moved.PreviousSemver.Should().Be("1.0.0");
        moved.PreviousChecksum.Should().Be("abc");

        var overwrite = RubricEditorLogic.PlanSave("1.0.0", "abc", "1.1.0", overwrite: true);
        overwrite.ExpectedChecksum.Should().BeNull();
        overwrite.Moving.Should().BeTrue();
    }

    [Fact]
    public void Adding_a_check_uses_the_add_type_and_not_the_open_check_type()
    {
        var post = new RubricEditorPost
        {
            Intent = "add-check",
            AddType = "FHIRPATH",
            Mode = "new",
            Id = "piqi.core",
            Semver = "1.0.0",
            ScoringType = "piqi-dimension-scorecard",
            Rollup = "worst-of",
            Dimensions = ["CONFORMANCE"],
            DefinitionJson = RubricJson.Write(RubricDraft.NewDefinition()),
            BaselineJson = RubricJson.Write(RubricDraft.NewDefinition()),
            Check = new RubricCheckPost { Type = "VALUESET" }
        };

        var state = RubricEditorLogic.Apply(post, null);
        state.Definition.Checks.Should().ContainSingle().Which.Type.Should().Be("FHIRPATH");
        state.EditIndex.Should().Be(0);
    }

    [Fact]
    public void Rubric_pages_use_the_configuration_shell_and_hold_confirm()
    {
        var root = RepoFile("DotNet/Link.UI/Views/Configuration");
        var nav = File.ReadAllText(Path.Combine(root, "_Nav.cshtml"));
        nav.Should().Contain("asp-action=\"Rubrics\"").And.Contain("Rubrics");
        File.ReadAllText(Path.Combine(root, "Index.cshtml")).Should().Contain("Validation rubrics");

        foreach (var name in new[] { "Rubrics.cshtml", "Rubric.cshtml", "RubricVersion.cshtml", "RubricEditor.cshtml", "RubricImport.cshtml", "RubricCompare.cshtml", "RubricDryRun.cshtml" })
        {
            var text = File.ReadAllText(Path.Combine(root, name));
            text.Should().Contain("_BackButton");
            text.Should().Contain("_Nav");
            text.Should().NotContain("nav-tabs");
            text.Should().NotContain("btn-primary");
            text.Should().NotContain("typeToConfirm");
        }

        var plan = File.ReadAllText(Path.Combine(root, "_RubricPlan.cshtml"));
        plan.Should().Contain("data-hold=");
        plan.Should().Contain("PublishRubric").And.Contain("RetireRubric").And.Contain("DeleteRubricDraft");
        plan.Should().NotContain("type=\"text\"");
        plan.Should().NotContain("typeToConfirm");

        var editor = File.ReadAllText(Path.Combine(root, "RubricEditor.cshtml"));
        editor.Should().Contain("name=\"addType\"");
        editor.Should().Contain("beforeunload");
        editor.Should().Contain("name=\"definitionJson\"");
        editor.IndexOf("value=\"save\"", StringComparison.Ordinal).Should().BeLessThan(editor.IndexOf("value=\"add-check\"", StringComparison.Ordinal));

        var import = File.ReadAllText(Path.Combine(root, "RubricImport.cshtml"));
        import.Should().Contain("value=\"review\"").And.Contain("value=\"review-next\"").And.Contain("value=\"import\"").And.Contain("value=\"open\"");

        var dryRun = File.ReadAllText(Path.Combine(root, "RubricDryRun.cshtml"));
        dryRun.Should().Contain("data-severity=").And.Contain("row.hidden");
        dryRun.Should().NotContain("form.submit");

        File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/ConfigurationController.Rubrics.cs"))
            .Should().Contain("[FromQuery(Name = \"do\")]");
    }

    private static RubricDetail Rubric() => new()
    {
        RubricId = "piqi.core",
        LatestPublishedSemver = "1.0.0",
        Versions = [new RubricVersionSummary { Semver = "1.0.0", Status = "PUBLISHED" }]
    };

    private static RubricDefinition Definition(string id, string semver) => new()
    {
        Id = id,
        Semver = semver,
        Title = "Core",
        Dimensions = ["CONFORMANCE"],
        ScoringPolicy = new RubricScoringPolicy { Type = "piqi-dimension-scorecard", Rollup = "worst-of" }
    };

    private static RubricCheck Check(string id, string expression) => new()
    {
        Id = id,
        Type = "FHIRPATH",
        Dimension = "CONFORMANCE",
        Enabled = true,
        Parameters = new RubricCheckParameters { Expression = expression }
    };

    private static RubricValidationResult Review(bool exists, string? status, string? checksum = "abc", string? existing = null) => new()
    {
        Valid = true,
        Definition = Definition("piqi.core", "1.0.0"),
        RubricExists = exists,
        ExistingStatus = status,
        Checksum = checksum,
        ExistingChecksum = existing
    };

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
