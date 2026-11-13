namespace Link.UI.Services;

/// <summary>
/// Page size for the facility template list. The size comes from the query string.
/// It is not a configuration setting.
/// </summary>
public static class FacilityTemplateListRules
{
    public const int DefaultPageSize = 25;
    public const int MaxBulkDelete = 100;

    public static readonly int[] PageSizes = [10, 25, 50, 100];
}
