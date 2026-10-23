namespace LantanaGroup.Link.PatientFlowSimulator.Scenario;

/// <summary>
/// An inclusive integer range a value is picked from.
/// </summary>
public sealed class RangeSpec
{
    /// <summary>
    /// Lowest value, inclusive.
    /// </summary>
    public int Min { get; set; }

    /// <summary>
    /// Highest value, inclusive.
    /// </summary>
    public int Max { get; set; }

    /// <summary>
    /// Throws when the range is empty or below <paramref name="minimum"/>.
    /// </summary>
    public void Validate(string what, int minimum)
    {
        if (Min < minimum || Max < Min)
        {
            throw new InvalidDataException($"{what} must satisfy {minimum} <= min <= max (got {Min}..{Max}).");
        }
    }
}
