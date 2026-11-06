using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>Each validation finding shows the profile it was validated against (LEGLINK-1172).</summary>
public class ValidationIssueProfileTests
{
    private const string Lab = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-observation-lab|6.1.0";

    [Fact]
    public void Results_carry_the_profile_and_findings_without_one_stay_empty()
    {
        var issues = ReportsRules.ParseIssues($$"""
            [
              { "patientId": "p-1", "severity": "ERROR", "code": "a", "message": "m", "profile": "{{Lab}}" },
              { "patientId": "p-2", "severity": "WARNING", "code": "b", "message": "m" }
            ]
            """);

        issues.Should().HaveCount(2);
        issues[0].Profile.Should().Be(Lab);
        issues[1].Profile.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Lab, "us-core-observation-lab|6.1.0")]
    [InlineData("http://example.org/StructureDefinition/thing", "thing")]
    [InlineData("http://example.org/StructureDefinition/thing/", "thing")]
    [InlineData("plain-name", "plain-name")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Profile_label_is_the_short_name_with_its_version(string? profile, string expected) =>
        ReportsRules.ProfileLabel(profile).Should().Be(expected);

    [Fact]
    public void Issues_sort_and_search_by_profile()
    {
        IReadOnlyList<ValidationIssueRow> issues =
        [
            new() { PatientId = "p-1", Severity = "ERROR", Code = "a", Profile = "http://x/StructureDefinition/zeta" },
            new() { PatientId = "p-2", Severity = "ERROR", Code = "b", Profile = "http://x/StructureDefinition/alpha" },
            new() { PatientId = "p-3", Severity = "ERROR", Code = "c" }
        ];

        ReportsRules.IssueSorts.Should().Contain("profile");
        var sorted = ReportsRules.SliceIssues(issues, new ValidationIssueQuery { Sort = "profile", Descending = false });
        sorted.Page.Select(issue => issue.PatientId).Should().Equal("p-3", "p-2", "p-1");

        var searched = ReportsRules.SliceIssues(issues, new ValidationIssueQuery { Text = "alpha" });
        searched.Page.Should().ContainSingle().Which.PatientId.Should().Be("p-2");
    }

    [Fact]
    public void Validation_issue_table_has_a_profile_column()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        var view = File.ReadAllText(Path.Combine(dir!.FullName, "Link.UI", "Views", "Reports", "Validation.cshtml"));

        view.Should().Contain("Profile@(Mark(\"profile\"))");
        view.Should().Contain("<td data-issue-profile");
        view.Should().Contain("@ReportsRules.ProfileLabel(issue.Profile)");
        view.Should().Contain("title=\"@issue.Profile\"");
    }
}
