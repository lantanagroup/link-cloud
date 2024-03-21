using static LantanaGroup.Link.LinkAdmin.BFF.Settings.LinkAdminConstants;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging
{
    public static partial class Logging
    {
        [LoggerMessage(
            LinkAdminLoggingIds.ApiRegistered,
            LogLevel.Information,
            "API {api} registered.")]
        public static partial void LogApiRegistration(this ILogger logger, string api);

        [LoggerMessage(
            LinkAdminLoggingIds.RequestRecieved,
            LogLevel.Information,
            "Request Recieved for {api} at {timestamp}.")]
        public static partial void LogRequestRecieved(this ILogger logger, string api, DateTime timestamp);

        [LoggerMessage(
            LinkAdminLoggingIds.RequestRecievedWarning,
            LogLevel.Warning,
            "Request Recieved for {api} at {timestamp}.")]
        public static partial void LogRequestRecievedWarning(this ILogger logger, string api, DateTime timestamp);

        [LoggerMessage(
            LinkAdminLoggingIds.RequestRecievedException,
            LogLevel.Critical,
            "Request Recieved for {api} at {timestamp}.")]
        public static partial void LogRequestRecievedException(this ILogger logger, string api, DateTime timestamp);
    }
}
