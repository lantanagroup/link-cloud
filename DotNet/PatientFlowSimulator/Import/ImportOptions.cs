namespace LantanaGroup.Link.PatientFlowSimulator.Import;

/// <summary>
/// What one import run needs to know.
/// </summary>
public sealed class ImportOptions
{
    /// <summary>
    /// Root of the generated data, one sub-directory per month.
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>
    /// Scenario file, used to generate a month that is missing.
    /// </summary>
    public required string ScenarioPath { get; init; }

    /// <summary>
    /// FHIR R4 base URL the transaction bundles are posted to.
    /// </summary>
    public required string FhirBaseUrl { get; init; }

    /// <summary>
    /// Static bearer token sent as Authorization: Bearer.
    /// </summary>
    public string? BearerToken { get; init; }

    /// <summary>
    /// user:password sent as Authorization: Basic.
    /// </summary>
    public string? BasicCredentials { get; init; }

    /// <summary>
    /// The instant the run considers "now". Files whose hour has fully elapsed by then are due.
    /// </summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Generate the current month's files when they do not exist yet.
    /// </summary>
    public bool GenerateMissingMonths { get; init; } = true;

    /// <summary>
    /// List what would be uploaded without uploading or marking anything.
    /// </summary>
    public bool DryRun { get; init; }
}
