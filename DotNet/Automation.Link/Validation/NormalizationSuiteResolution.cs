using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// The result of resolving a normalization suite.
/// </summary>
public record NormalizationSuiteResolution(
    string SuiteName,
    List<NormalizationOperationDefinition> Operations,
    List<NormalizationSuiteSequenceResolution> Sequences,
    List<NormalizationOperationDefinition> StandaloneOperations)
{
    public Guid? SuiteId { get; init; }
}

public record NormalizationSuiteSequenceResolution(
    string SequenceName,
    List<NormalizationSuiteSequenceOperationResolution> Operations);

public record NormalizationSuiteSequenceOperationResolution(
    int Sequence,
    NormalizationOperationDefinition Operation);
