using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;

namespace UnitTests.Report;

/// <summary>
/// Covers the one indicator the API reports but storage does not hold: whether the patient is in the
/// report at all, which decides whether any code map result about them is worth showing.
/// </summary>
[Trait("Category", "UnitTests")]
public class MappingIndicatorViewTests
{
    [Fact]
    public void NoEncounterBelongedToTheOrganization_IsExcluded()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.NotEvaluated,
            locationOrgStatus: MappingIndicatorStatus.Unmapped);

        // The patient's encounters were all stripped, so the measure evaluates no qualifying encounter for
        // them. Left as NotEvaluated the row would sit there forever looking like it was still in flight,
        // with nothing to say why.
        Assert.Equal(MappingIndicatorStatus.Excluded, status);
    }

    [Theory]
    [InlineData(MappingIndicatorStatus.Mapped)]
    [InlineData(MappingIndicatorStatus.PartiallyMapped)]
    [InlineData(MappingIndicatorStatus.Unmapped)]
    [InlineData(MappingIndicatorStatus.NothingToEvaluate)]
    [InlineData(MappingIndicatorStatus.NotApplicable)]
    [InlineData(MappingIndicatorStatus.Unknown)]
    public void ExcludedOutranksAnyResultNormalizationReported(MappingIndicatorStatus stored)
    {
        var status = MappingIndicatorView.ResolveHsloc(stored, MappingIndicatorStatus.Unmapped);

        // Stripping a patient's encounters does not necessarily empty the correlation: the Location
        // resources they referenced survive and are code mapped perfectly well, so Normalization can
        // report a genuine Mapped for a patient who is not in the report. Surfacing it would describe a
        // location the report never evaluates and read as a clean pass for an excluded patient.
        Assert.Equal(MappingIndicatorStatus.Excluded, status);
    }

    [Theory]
    [InlineData(MappingIndicatorStatus.Mapped)]
    [InlineData(MappingIndicatorStatus.PartiallyMapped)]
    [InlineData(MappingIndicatorStatus.NothingToEvaluate)]
    [InlineData(MappingIndicatorStatus.NotApplicable)]
    [InlineData(MappingIndicatorStatus.Unknown)]
    public void PatientInTheOrganization_KeepsTheStoredResult(MappingIndicatorStatus stored)
    {
        var status = MappingIndicatorView.ResolveHsloc(stored, MappingIndicatorStatus.Mapped);

        // The exclusion is the only override. For a patient the report does evaluate, the code map result
        // is the answer and must not be rewritten from another column.
        Assert.Equal(stored, status);
    }

    [Fact]
    public void MembershipAssumedRatherThanVerified_IsStillInTheReport()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.NothingToEvaluate,
            locationOrgStatus: MappingIndicatorStatus.Assumed);

        // Assumed means membership was never checked, not that it failed. Such a patient still ships in
        // the submission, so their code map result is still worth reporting.
        Assert.Equal(MappingIndicatorStatus.NothingToEvaluate, status);
    }

    [Fact]
    public void AcquisitionHasNotReported_LeavesTheStoredValueAlone()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.NotEvaluated,
            locationOrgStatus: MappingIndicatorStatus.NotEvaluated);

        // Excluded is derived from what acquisition found. With acquisition silent there is nothing to
        // derive it from, and the honest answer is that nothing is known yet.
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, status);
    }

    [Fact]
    public void OrgLocationMappingNotConfigured_DoesNotExcludeAnyone()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.PartiallyMapped,
            locationOrgStatus: MappingIndicatorStatus.NotApplicable);

        // A facility that has not configured org-location resolution excludes nobody -- every patient is
        // evaluated. Treating NotApplicable as an exclusion would blank the HSLOC column for every patient
        // at that facility.
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, status);
    }
}
