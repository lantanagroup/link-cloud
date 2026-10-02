using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Validation;

namespace UnitTests.AutomationLink;

[Trait("Category", "UnitTests")]
public class DataAcquisitionDatabaseValidatorHierarchyTests
{
    [Fact]
    public void Dangling_parent_proven_absent_is_not_a_hierarchy_gap()
    {
        var errors = new List<string>();

        DataAcquisitionDatabaseValidator.ValidateLocationHierarchy(
            [Mapping("room", "missing-parent", isOrg: true)],
            errors,
            new HashSet<string>(StringComparer.Ordinal) { "missing-parent" });

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Missing_parent_row_is_still_a_hierarchy_gap()
    {
        var errors = new List<string>();

        DataAcquisitionDatabaseValidator.ValidateLocationHierarchy(
            [Mapping("room", "real-parent", isOrg: true)],
            errors);

        errors.Should().ContainSingle()
            .Which.Should().Contain("real-parent").And.Contain("hierarchy gap");
    }

    [Fact]
    public void Parent_row_present_is_not_a_gap_even_when_the_id_was_recorded_absent()
    {
        var errors = new List<string>();

        DataAcquisitionDatabaseValidator.ValidateLocationHierarchy(
            [
                Mapping("room", "wing", isOrg: true),
                Mapping("wing", partOf: null, isOrg: true)
            ],
            errors,
            new HashSet<string>(StringComparer.Ordinal) { "wing" });

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Absent_parent_does_not_hide_another_gap_or_a_non_org_ancestor()
    {
        var errors = new List<string>();

        DataAcquisitionDatabaseValidator.ValidateLocationHierarchy(
            [
                Mapping("dangling-room", "missing-parent", isOrg: true),
                Mapping("other-room", "unmapped-parent", isOrg: true),
                Mapping("org-child", "plain-parent", isOrg: true),
                Mapping("plain-parent", partOf: null, isOrg: false)
            ],
            errors,
            new HashSet<string>(StringComparer.Ordinal) { "missing-parent" });

        errors.Should().HaveCount(2);
        errors.Should().Contain(error => error.Contains("unmapped-parent", StringComparison.Ordinal) && error.Contains("hierarchy gap", StringComparison.Ordinal));
        errors.Should().Contain(error => error.Contains("plain-parent", StringComparison.Ordinal) && error.Contains("ancestor mismatch", StringComparison.Ordinal));
        errors.Should().NotContain(error => error.Contains("missing-parent", StringComparison.Ordinal));
    }

    [Fact]
    public void Cycle_is_still_reported()
    {
        var errors = new List<string>();

        DataAcquisitionDatabaseValidator.ValidateLocationHierarchy(
            [
                Mapping("a", "b", isOrg: true),
                Mapping("b", "a", isOrg: true)
            ],
            errors);

        errors.Should().Contain(error => error.Contains("cycle", StringComparison.Ordinal));
    }

    private static PipelineDataReader.OrganizationLocationMappingInfo Mapping(string id, string? partOf, bool isOrg) =>
        new("facility", id, isOrg, true, partOf);
}
