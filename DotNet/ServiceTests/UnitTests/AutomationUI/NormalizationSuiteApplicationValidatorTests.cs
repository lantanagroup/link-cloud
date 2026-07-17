using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Helpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationSuiteApplicationValidatorTests
{
    [Fact]
    public async Task NonOptionalOperation_NoEvidence_FailsValidation()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildSuiteWithSingleOperation("OpNoEvidence", sequence: 1);
        var abs = BuildAbs("loc-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, []));
    }

    [Fact]
    public async Task NonOptionalOperation_PartialEvidenceCoverage_FailsValidation()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildSuiteWithSingleOperation("OpPartial", sequence: 1);
        var abs = BuildAbs("loc-1", "loc-2");
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyProperty:OpPartial:Success]"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, logs));
    }

    [Fact]
    public async Task NonOptionalOperation_AllNoAction_FailsValidation()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildSuiteWithSingleOperation("OpNoAction", sequence: 1);
        var abs = BuildAbs("loc-1");
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyProperty:OpNoAction:NoAction]"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, logs));
    }

    [Fact]
    public async Task NonOptionalOperation_AllFailure_FailsValidation()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildSuiteWithSingleOperation("OpFailure", sequence: 1);
        var abs = BuildAbs("loc-1");
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyProperty:OpFailure:Failure]"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, logs));
    }

    [Fact]
    public async Task NonOptionalOperation_WithSuccessEvidence_PassesValidation()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildSuiteWithSingleOperation("OpSuccess", sequence: 1);
        var abs = BuildAbs("loc-1");
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyProperty:OpSuccess:Success]"
        };

        await sut.ValidateAllAsync(abs, suite, logs);
    }

    [Fact]
    public async Task Repeated_operation_name_uses_matching_sequence_evidence_and_can_fail_independently()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var op1 = BuildCopyPropertyOperation("RepeatOp");
        var op2 = BuildCopyPropertyOperation("RepeatOp");

        var suite = new NormalizationSuiteResolution(
            "Test Suite",
            [op1, op2],
            [
                new NormalizationSuiteSequenceResolution(
                    "Seq",
                    [
                        new NormalizationSuiteSequenceOperationResolution(1, op1),
                        new NormalizationSuiteSequenceOperationResolution(2, op2)
                    ])
            ],
            []);

        var abs = new Dictionary<string, object>
        {
            ["location.ndjson"] = "{\"resourceType\":\"Location\",\"id\":\"loc-1\"}\n"
        };

        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyProperty:RepeatOp:Failure | 2:CopyProperty:RepeatOp:Success]"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, logs));

        ex.Message.Should().Contain("NORMALIZATION SUITE APPLICATION VALIDATION failed");
        output.Lines.Should().Contain(l => l.Contains("only shows Failure outcomes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Repeated_operation_name_passes_when_each_sequence_has_its_own_success_evidence()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var op1 = BuildCopyPropertyOperation("RepeatOp");
        var op2 = BuildCopyPropertyOperation("RepeatOp");

        var suite = new NormalizationSuiteResolution(
            "Test Suite",
            [op1, op2],
            [
                new NormalizationSuiteSequenceResolution(
                    "Seq",
                    [
                        new NormalizationSuiteSequenceOperationResolution(1, op1),
                        new NormalizationSuiteSequenceOperationResolution(2, op2)
                    ])
            ],
            []);

        var abs = new Dictionary<string, object>
        {
            ["location.ndjson"] = "{\"resourceType\":\"Location\",\"id\":\"loc-1\"}\n"
        };

        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyProperty:RepeatOp:Success | 2:CopyProperty:RepeatOp:Success]"
        };

        await sut.ValidateAllAsync(abs, suite, logs);

        output.Lines.Should().Contain(l => l.Contains("NORMALIZATION SUITE APPLICATION VALIDATION: Passed", StringComparison.Ordinal));
    }

    private static NormalizationOperationDefinition BuildCopyPropertyOperation(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        OperationType = "CopyProperty",
        ResourceTypes = ["Location"],
        SourceFhirPath = "identifier.value",
        TargetFhirPath = "type[0].coding.code"
    };

    private static NormalizationSuiteResolution BuildSuiteWithSingleOperation(string operationName, int sequence)
    {
        var op = BuildCopyPropertyOperation(operationName);
        return new NormalizationSuiteResolution(
            "Test Suite",
            [op],
            [
                new NormalizationSuiteSequenceResolution(
                    "Seq",
                    [new NormalizationSuiteSequenceOperationResolution(sequence, op)])
            ],
            []);
    }

    private static Dictionary<string, object> BuildAbs(params string[] ids)
    {
        var lines = string.Join("\n", ids.Select(id => $"{{\"resourceType\":\"Location\",\"id\":\"{id}\"}}")) + "\n";
        return new Dictionary<string, object>
        {
            ["location.ndjson"] = lines
        };
    }

    private sealed class CapturingOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
