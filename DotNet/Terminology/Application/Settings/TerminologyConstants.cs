namespace LantanaGroup.Link.Terminology.Application.Settings;

/// <summary>
/// Provides constant values related to the Terminology application
/// for use throughout the application, including service name and application
/// settings section names.
/// </summary>
public abstract class TerminologyConstants
{
    public const string ServiceName = "Terminology";

    public static class AppSettingsSectionNames
    {
        public const string Terminology = "Terminology";
        public const string ServiceInformation = "ServiceInformation";
        public const string Serilog = "Serilog";
    }
}