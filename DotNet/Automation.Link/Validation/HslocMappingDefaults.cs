using LantanaGroup.Automation.Generation.ResourceFactories;

namespace LantanaGroup.Link.Automation.Link.Validation;

public static class HslocMappingDefaults
{
    public const string OperationName = "HSLOC Location Mapping";
    public const string OperationType = "HSLOCMap";
    public const string FhirPath = "type";
    public const string IdentifierSystem = LocationFactory.IdentifierSystem;
    public const string RoleCodeSystem = LocationFactory.RoleCodeSystem;

    public static readonly IReadOnlyDictionary<string, (string Code, string Display)> RoleCodeToHsloc =
        new Dictionary<string, (string Code, string Display)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ICU"] = ("1025-6", "Trauma Critical Care"),
            ["ER"] = ("1108-0", "Emergency Department"),
            ["HU"] = ("1093-4", "Step Down Unit"),
            ["HOSP"] = ("1060-3", "Medical Ward"),
            ["OF"] = ("1160-1", "Urgent Care Center")
        };
}
