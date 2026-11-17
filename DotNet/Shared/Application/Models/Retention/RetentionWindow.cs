namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// A retention window, applied in UTC with calendar arithmetic. It is never converted to a fixed number of
/// days, so three months is three calendar months.
/// </summary>
public class RetentionWindow
{
    /// <summary>
    /// Whether <see cref="Count"/> is days or months.
    /// </summary>
    public required RetentionWindowUnit Unit { get; set; }

    /// <summary>
    /// How many units the window spans. Greater than zero.
    /// </summary>
    public required int Count { get; set; }
}
