using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;

namespace UnitTests.Report;

/// <summary>
/// Covers the one indicator the API reports but storage does not hold: whether Normalization is never
/// going to answer for this patient, as opposed to not having answered yet.
/// </summary>
[Trait("Category", "UnitTests")]
public class MappingIndicatorViewTests
{
    [Fact]
    public void NormalizationNeverReportedAndNoEncounterWasInTheOrg_IsExcluded()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.NotEvaluated,
            locationOrgStatus: MappingIndicatorStatus.Unmapped,
            normalizationEvaluatedAt: null);

        // Acquisition strips every non-org encounter, so a patient with none left never reaches
        // Normalization and no message is ever produced. Left as NotEvaluated the row would sit there
        // forever looking like it was still in flight, with nothing to say why.
        Assert.Equal(MappingIndicatorStatus.Excluded, status);
    }

    [Fact]
    public void NormalizationHasNotReportedButThePatientIsInTheOrg_StaysNotEvaluated()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.NotEvaluated,
            locationOrgStatus: MappingIndicatorStatus.Mapped,
            normalizationEvaluatedAt: null);

        // This patient's resources did survive the strip, so Normalization still owes an answer. Calling
        // it Excluded would be a guess that goes stale the moment the message lands.
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, status);
    }

    [Fact]
    public void NeitherSourceHasReported_StaysNotEvaluated()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.NotEvaluated,
            locationOrgStatus: MappingIndicatorStatus.NotEvaluated,
            normalizationEvaluatedAt: null);

        // Excluded is derived from what acquisition found. With acquisition silent there is nothing to
        // derive it from, and the honest answer is that nothing is known yet.
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, status);
    }

    [Theory]
    [InlineData(MappingIndicatorStatus.Mapped)]
    [InlineData(MappingIndicatorStatus.PartiallyMapped)]
    [InlineData(MappingIndicatorStatus.Unmapped)]
    [InlineData(MappingIndicatorStatus.NothingToEvaluate)]
    [InlineData(MappingIndicatorStatus.NotApplicable)]
    [InlineData(MappingIndicatorStatus.Unknown)]
    public void NormalizationReported_ItsAnswerStands(MappingIndicatorStatus stored)
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored,
            locationOrgStatus: MappingIndicatorStatus.Unmapped,
            normalizationEvaluatedAt: DateTime.UtcNow);

        // The acquisition side is only ever used to explain an ABSENT answer. Once Normalization has
        // spoken, overriding it from another column would be inventing a result.
        Assert.Equal(stored, status);
    }

    [Fact]
    public void NormalizationReportedNothingForAnExcludedLookingPatient_IsNotRewritten()
    {
        var status = MappingIndicatorView.ResolveHsloc(
            stored: MappingIndicatorStatus.NotApplicable,
            locationOrgStatus: MappingIndicatorStatus.Unmapped,
            normalizationEvaluatedAt: DateTime.UtcNow);

        // The boundary between the two rules. Both conditions for Excluded look satisfied except the one
        // that matters: Normalization did run, so NotApplicable is a real result and not an absence.
        Assert.Equal(MappingIndicatorStatus.NotApplicable, status);
    }
}
