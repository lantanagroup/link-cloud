
namespace LantanaGroup.Link.Normalization.Application.Settings;

public static class NormalizationConstants
{
    public const string ServiceName = "Normalization";

    public static class AppSettingsSectionNames
    {
        public const string ServiceInformation = "ServiceInformation";
        public const string DatabaseProvider = "DatabaseProvider";
    }

    public static class HeaderNames
    {
        public const string CorrelationId = "X-Correlation-Id";
    }
}
