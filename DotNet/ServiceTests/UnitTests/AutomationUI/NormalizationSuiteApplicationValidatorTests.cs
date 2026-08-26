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
    public async Task RemoveExtensions_NoLokiEvidence_FailsValidation()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildRemoveExtensionsSuite("Remove Observation Datetime Extension", "Observation");
        var abs = new Dictionary<string, object>
        {
            ["patient.ndjson"] = "{\"resourceType\":\"Observation\",\"id\":\"obs-1\",\"status\":\"final\"}\n"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, []));
    }

    [Fact]
    public async Task RemoveExtensions_PatientNotInQueryPlan_SkipsLokiEvidenceRequirement()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildRemoveExtensionsSuite("Remove Common Extensions", "Patient");
        var abs = new Dictionary<string, object>
        {
            ["patient.ndjson"] = "{\"resourceType\":\"Patient\",\"id\":\"p-1\"}\n"
        };

        await sut.ValidateAllAsync(abs, suite, [], acquiredResourceTypes: ["Encounter", "Observation"]);

        output.Lines.Should().Contain(l => l.Contains("NORMALIZATION SUITE APPLICATION VALIDATION: Passed", StringComparison.Ordinal));
        output.Lines.Should().Contain(l =>
            l.Contains("not an acquired query-plan type", StringComparison.Ordinal)
            && l.Contains("Patient", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RemoveExtensions_AcquiredTypeWithoutEvidence_StillFails()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildRemoveExtensionsSuite("Remove Common Extensions", "Observation");
        var abs = new Dictionary<string, object>
        {
            ["patient.ndjson"] = "{\"resourceType\":\"Observation\",\"id\":\"obs-1\"}\n"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, [], acquiredResourceTypes: ["Encounter", "Observation"]));
    }

    [Fact]
    public async Task RemoveExtensions_SuccessEvidence_PassesWithoutScanningEveryResource()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildRemoveExtensionsSuite("Remove Observation Datetime Extension", "Observation");
        var abs = new Dictionary<string, object>
        {
            ["patient.ndjson"] =
                "{\"resourceType\":\"Observation\",\"id\":\"obs-1\",\"status\":\"final\"}\n" +
                "{\"resourceType\":\"Observation\",\"id\":\"obs-2\",\"status\":\"final\"}\n"
        };
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Observation, ResourceId=obs-1, Steps=[1:RemoveExtensions:Remove Observation Datetime Extension:Success]"
        };

        await sut.ValidateAllAsync(abs, suite, logs);

        output.Lines.Should().Contain(l => l.Contains("NORMALIZATION SUITE APPLICATION VALIDATION: Passed", StringComparison.Ordinal));
        output.Lines.Should().Contain(l => l.Contains("Evidence found", StringComparison.Ordinal));
        output.Lines.Should().Contain(l => l.Contains("Success=1", StringComparison.Ordinal));
        output.Lines.Should().NotContain(l => l.Contains("no forbidden extensions remained", StringComparison.Ordinal));
        output.Lines.Should().NotContain(l => l.Contains("did not include all ABS candidates", StringComparison.Ordinal));
    }

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
    public async Task NonOptionalOperation_AllNoAction_PassesValidation()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildSuiteWithSingleOperation("OpNoAction", sequence: 1);
        var abs = BuildAbs("loc-1");
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyProperty:OpNoAction:NoAction]"
        };

        await sut.ValidateAllAsync(abs, suite, logs);

        output.Lines.Should().Contain(l => l.Contains("NORMALIZATION SUITE APPLICATION VALIDATION: Passed", StringComparison.Ordinal));
        output.Lines.Should().Contain(l => l.Contains("NoAction=1", StringComparison.Ordinal));
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

    [Fact]
    public async Task ExtendedSuite_GeneratedLocationOp_MatchesRuntimeSequenceNotSuiteSequence()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var copyLocation = BuildCopyPropertyOperation("Copy Location Identifiers to Type", "CopyLocation");
        var copyProperty = BuildCopyPropertyOperation("Copy Location Identifier Value to Type Code");
        var copyAlias = BuildCopyPropertyOperation("Copy Location aliases to type iteratively", "CopyLocationAliasToTypeIteratively");
        var transform = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Set Encounter status when class matches upload",
            OperationType = "ConditionalTransform",
            ResourceTypes = ["Encounter"],
            ConditionTargetFhirPath = "status",
            ConditionTargetValue = "in-progress"
        };

        var suite = new NormalizationSuiteResolution(
            "System Default extended",
            [copyLocation, copyProperty, copyAlias, transform],
            [
                new NormalizationSuiteSequenceResolution(
                    "Default Location Normalization",
                    [
                        new NormalizationSuiteSequenceOperationResolution(1, copyLocation),
                        new NormalizationSuiteSequenceOperationResolution(2, copyProperty)
                    ]),
                new NormalizationSuiteSequenceResolution(
                    "Generated patient sequence",
                    [
                        new NormalizationSuiteSequenceOperationResolution(1, copyAlias),
                        new NormalizationSuiteSequenceOperationResolution(2, transform)
                    ])
            ],
            []);

        var abs = new Dictionary<string, object>
        {
            ["location.ndjson"] = "{\"resourceType\":\"Location\",\"id\":\"loc-1\"}\n",
            ["encounter.ndjson"] = "{\"resourceType\":\"Encounter\",\"id\":\"enc-1\"}\n"
        };
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyLocation:Copy Location Identifiers to Type:Success | 2:CopyProperty:Copy Location Identifier Value to Type Code:Success | 3:CopyLocationAliasToTypeIteratively:Copy Location aliases to type iteratively:Success]",
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Encounter, ResourceId=enc-1, Steps=[1:ConditionalTransform:Set Encounter status when class matches upload:Success]"
        };

        await sut.ValidateAllAsync(abs, suite, logs);

        output.Lines.Should().Contain(l => l.Contains("NORMALIZATION SUITE APPLICATION VALIDATION: Passed", StringComparison.Ordinal));
        output.Lines.Should().Contain(l =>
            l.Contains("runtime Location#3", StringComparison.Ordinal)
            && l.Contains("Copy Location aliases to type iteratively", StringComparison.Ordinal));
        output.Lines.Should().Contain(l =>
            l.Contains("runtime Encounter#1", StringComparison.Ordinal)
            && l.Contains("Set Encounter status when class matches upload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonOptionalOperation_EvidenceAtWrongRuntimeSequence_FailsAndLogsNearby()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suite = BuildSuiteWithSingleOperation("Copy Location aliases to type iteratively", sequence: 1, operationType: "CopyLocationAliasToTypeIteratively");
        var abs = BuildAbs("loc-1");
        var logs = new List<string>
        {
            "[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyLocation:Copy Location Identifiers to Type:Success | 3:CopyLocationAliasToTypeIteratively:Copy Location aliases to type iteratively:Success]"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync(abs, suite, logs));

        output.Lines.Should().Contain(l =>
            l.Contains("runtime sequence=1", StringComparison.Ordinal)
            && l.Contains("Same name/type observed at sequence=3 (Success=1)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonOptionalOperation_EmDashName_MatchesSanitizeForLogEvidence()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var suiteName = "Live Patient Test cleanup — Copy Location aliases to type iteratively";
        var loggedName = "Live Patient Test cleanup   Copy Location aliases to type iteratively";
        var suite = BuildSuiteWithSingleOperation(suiteName, sequence: 1, operationType: "CopyLocationAliasToTypeIteratively");
        var abs = BuildAbs("loc-1");
        var logs = new List<string>
        {
            $"[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CopyLocationAliasToTypeIteratively:{loggedName}:Success]"
        };

        await sut.ValidateAllAsync(abs, suite, logs);

        output.Lines.Should().Contain(l => l.Contains("NORMALIZATION SUITE APPLICATION VALIDATION: Passed", StringComparison.Ordinal));
        output.Lines.Should().Contain(l => l.Contains("Evidence found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonOptionalOperation_CodeMapNameWithColons_MatchesEvidence()
    {
        var output = new CapturingOutput();
        var sut = new NormalizationSuiteApplicationValidator(output);

        var name = "Code map Location.type (http://terminology.hl7.org/CodeSystem/v3-RoleCode)";
        var op = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            OperationType = "CodeMap",
            ResourceTypes = ["Location"],
            CodeMapFhirPath = "type.coding"
        };
        var suite = new NormalizationSuiteResolution(
            "Test Suite",
            [op],
            [
                new NormalizationSuiteSequenceResolution(
                    "Generated",
                    [new NormalizationSuiteSequenceOperationResolution(3, op)])
            ],
            []);
        var abs = BuildAbs("loc-1");
        var logs = new List<string>
        {
            $"[NormalizationExecutionSummary] FacilityId=f1, CorrelationId=c1, PatientId=p1, ResourceType=Location, ResourceId=loc-1, Steps=[1:CodeMap:{name}:Success]"
        };

        await sut.ValidateAllAsync(abs, suite, logs);

        output.Lines.Should().Contain(l => l.Contains("NORMALIZATION SUITE APPLICATION VALIDATION: Passed", StringComparison.Ordinal));
        output.Lines.Should().Contain(l => l.Contains("runtime Location#1", StringComparison.Ordinal));
    }

    private static NormalizationSuiteResolution BuildRemoveExtensionsSuite(string name, string resourceType)
    {
        var op = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            OperationType = "RemoveExtensions",
            ResourceTypes = [resourceType],
            ExtensionUrls = ["http://open.epic.com/FHIR/StructureDefinition/extension/observation-datetime"]
        };
        return new NormalizationSuiteResolution(
            "Test Suite",
            [op],
            [
                new NormalizationSuiteSequenceResolution(
                    "Cleanup",
                    [new NormalizationSuiteSequenceOperationResolution(1, op)])
            ],
            []);
    }

    private static NormalizationOperationDefinition BuildCopyPropertyOperation(string name, string operationType = "CopyProperty") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        OperationType = operationType,
        ResourceTypes = ["Location"],
        SourceFhirPath = "identifier.value",
        TargetFhirPath = "type[0].coding.code"
    };

    private static NormalizationSuiteResolution BuildSuiteWithSingleOperation(
        string operationName,
        int sequence,
        string operationType = "CopyProperty")
    {
        var op = BuildCopyPropertyOperation(operationName, operationType);
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
