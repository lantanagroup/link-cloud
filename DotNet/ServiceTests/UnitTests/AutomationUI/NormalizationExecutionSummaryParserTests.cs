using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationExecutionSummaryParserTests
{
    [Fact]
    public void ParseLine_splits_operation_name_that_contains_colons()
    {
        var name = "Code map Location.type (http://terminology.hl7.org/CodeSystem/v3-RoleCode)";
        var line =
            $"[NormalizationExecutionSummary] FacilityId=f1, PatientId=p1, CorrelationId=c1, ReportTrackingId=r1, ResourceType=Location, ResourceId=loc-1, Steps=[4:CodeMap:{name}:NoAction]";

        var steps = NormalizationExecutionSummaryParser.ParseLine(line);

        steps.Should().ContainSingle();
        steps[0].Sequence.Should().Be(4);
        steps[0].OperationType.Should().Be("CodeMap");
        steps[0].OperationName.Should().Be(name);
        steps[0].Outcome.Should().Be("NoAction");
        steps[0].ResourceType.Should().Be("Location");
        steps[0].ResourceId.Should().Be("loc-1");
    }

    [Fact]
    public void ParseLine_keeps_pipe_separated_steps_and_simple_names()
    {
        var line =
            "[NormalizationExecutionSummary] FacilityId=f1, PatientId=p1, CorrelationId=c1, ReportTrackingId=r1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyLocation:Copy Location Identifiers to Type:Success | 3:CopyLocationAliasToTypeIteratively:Copy Location aliases to type iteratively:Success]";

        var steps = NormalizationExecutionSummaryParser.ParseLine(line);

        steps.Should().HaveCount(2);
        steps[0].Sequence.Should().Be(1);
        steps[0].OperationType.Should().Be("CopyLocation");
        steps[0].Outcome.Should().Be("Success");
        steps[1].Sequence.Should().Be(3);
        steps[1].OperationType.Should().Be("CopyLocationAliasToTypeIteratively");
        steps[1].OperationName.Should().Be("Copy Location aliases to type iteratively");
    }

    [Fact]
    public void NamesMatch_treats_sanitized_em_dash_as_equal()
    {
        var suiteName = "Live Patient Test cleanup — Code map Location.type (http://hospital.example.org/locations)";
        var loggedName = "Live Patient Test cleanup   Code map Location.type (http://hospital.example.org/locations)";

        NormalizationExecutionSummaryParser.NamesMatch(suiteName, loggedName).Should().BeTrue();
        NormalizationExecutionSummaryParser.NamesMatch(suiteName, suiteName).Should().BeTrue();
        NormalizationExecutionSummaryParser.NamesMatch(suiteName, "some other op").Should().BeFalse();
    }
}
