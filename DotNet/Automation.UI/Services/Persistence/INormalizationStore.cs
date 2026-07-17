using Automation.UI.Models;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Persistence contract for normalization operations, sequences, and suites.
/// </summary>
public interface INormalizationStore
{
    // --- Operations ---
    Task<List<NormalizationOperationDefinition>> GetAllOperationsAsync(CancellationToken ct = default);
    Task<NormalizationOperationDefinition?> GetOperationByIdAsync(Guid id, CancellationToken ct = default);
    Task UpsertOperationAsync(NormalizationOperationDefinition op, CancellationToken ct = default);
    Task DeleteOperationAsync(Guid id, CancellationToken ct = default);

    // --- Sequences ---
    Task<List<NormalizationSequenceDefinition>> GetAllSequencesAsync(CancellationToken ct = default);
    Task<NormalizationSequenceDefinition?> GetSequenceByIdAsync(Guid id, CancellationToken ct = default);
    Task UpsertSequenceAsync(NormalizationSequenceDefinition seq, CancellationToken ct = default);
    Task DeleteSequenceAsync(Guid id, CancellationToken ct = default);

    // --- Suites ---
    Task<List<NormalizationSuiteDefinition>> GetAllSuitesAsync(CancellationToken ct = default);
    Task<NormalizationSuiteDefinition?> GetSuiteByIdAsync(Guid id, CancellationToken ct = default);
    Task<NormalizationSuiteDefinition?> GetDefaultSuiteAsync(CancellationToken ct = default);
    Task UpsertSuiteAsync(NormalizationSuiteDefinition suite, CancellationToken ct = default);
    Task SetDefaultSuiteAsync(Guid id, CancellationToken ct = default);
    Task DeleteSuiteAsync(Guid id, CancellationToken ct = default);
}
