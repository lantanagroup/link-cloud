namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// A single section of a diagnostic snapshot (e.g., "Report State", "Data Acquisition", etc.).
/// Platform-specific code implements concrete sections.
/// </summary>
public interface IDiagnosticSnapshotSection
{
    /// <summary>
    /// Section name for the snapshot output.
    /// </summary>
    string SectionName { get; }

    /// <summary>
    /// Writes the section's diagnostic information to the output.
    /// Implementations should NOT throw — wrap errors and write them as diagnostics.
    /// </summary>
    Task WriteAsync(IAutomationOutput output, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs a collection of <see cref="IDiagnosticSnapshotSection"/>s and writes a
/// formatted diagnostic snapshot to test output.
/// </summary>
public sealed class DiagnosticSnapshotWriter
{
    private readonly IAutomationOutput _output;
    private readonly List<IDiagnosticSnapshotSection> _sections;

    public DiagnosticSnapshotWriter(IAutomationOutput output, IEnumerable<IDiagnosticSnapshotSection> sections)
    {
        _output = output;
        _sections = sections.ToList();
    }

    public DiagnosticSnapshotWriter(IAutomationOutput output, params IDiagnosticSnapshotSection[] sections)
        : this(output, sections.AsEnumerable())
    {
    }

    /// <summary>
    /// Writes the full diagnostic snapshot.
    /// </summary>
    public async Task WriteFullSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _output.WriteLine("\n=== PIPELINE DIAGNOSTIC SNAPSHOT ===\n");

        foreach (var section in _sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await section.WriteAsync(_output, cancellationToken);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[Snapshot][{section.SectionName}] Error: {ex.Message}");
            }
        }

        _output.WriteLine("\n=== END SNAPSHOT ===\n");
    }
}
