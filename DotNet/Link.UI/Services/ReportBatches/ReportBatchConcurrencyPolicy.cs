namespace Link.UI.Services.ReportBatches;

/// <summary>
/// The saved cap is a positive whole number. A batch may use a lower cap.
/// It cannot use a higher one. Nothing is read from a config file.
/// </summary>
public static class ReportBatchConcurrencyPolicy
{
    public const int DefaultMaxConcurrency = 2;
    public const int UpperBound = 8;
    public const int MaxScenariosPerBatch = 100;

    public static string? Validate(int value)
    {
        if (value < 1 || value > UpperBound)
            return $"Max concurrency must be a whole number from 1 to {UpperBound}.";

        return null;
    }

    /// <summary>
    /// Cap for one new batch. <paramref name="requested"/> null uses the saved cap.
    /// </summary>
    public static int ResolveBatchCap(int? requested, int savedCap)
    {
        if (Validate(savedCap) != null)
            savedCap = DefaultMaxConcurrency;

        if (requested is null)
            return savedCap;

        var error = Validate(requested.Value);
        if (error != null)
            throw new InvalidOperationException(error);

        if (requested.Value > savedCap)
        {
            throw new InvalidOperationException(
                $"maxConcurrency cannot be above the saved cap of {savedCap}.");
        }

        return requested.Value;
    }
}
