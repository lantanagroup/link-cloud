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
            LogLevel.Error,
            "Request Recieved for {api} at {timestamp}.")]
        public static partial void LogRequestRecievedException(this ILogger logger, string api, DateTime timestamp);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkAdminTokenGenerated,
            LogLevel.Information,
            "Link Admin Token was generated at {timestamp} for user {userId}.")]
        public static partial void LogLinkAdminTokenGenerated(this ILogger logger, DateTime timestamp, string userId);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkAdminTokenGenerationException,
            LogLevel.Error,
            "An exception occured while generating the Link Admin Token: {exception}")]
        public static partial void LogLinkAdminTokenGenerationException(this ILogger logger, string exception);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkAdminTokenKeyRefreshed,
            LogLevel.Information,
            "Link Admin Token Key was refreshed at {timestamp}.")]
        public static partial void LogLinkAdminTokenKeyRefreshed(this ILogger logger, DateTime timestamp);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkAdminTokenKeyRefreshException,
            LogLevel.Error,
            "An exception occured while refreshing the Link Admin Token Key: {exception}")]
        public static partial void LogLinkAdminTokenKeyRefreshException(this ILogger logger, string exception);

        [LoggerMessage(
            LinkAdminLoggingIds.KafkaProducerCreated,
            LogLevel.Information,
            "Kafka Producer for {topic} was created at {timestamp}.")]
        public static partial void LogKafkaProducerCreation(this ILogger logger, string topic, DateTime timestamp);

        [LoggerMessage(
            LinkAdminLoggingIds.KafkaProducerException,
            LogLevel.Critical,
            "An exception occured in the Kafka Producer for {topic}: {exception}")]
        public static partial void LogKafkaProducerException(this ILogger logger, string topic, string exception);

        [LoggerMessage(
            LinkAdminLoggingIds.KafkaProducerPatientEvent,
            LogLevel.Information,
            "New Patient Event with a correlation id of {correlationId} was created.")]
        public static partial void LogKafkaProducerPatientEvent(this ILogger logger, string correlationId);

        [LoggerMessage(
            LinkAdminLoggingIds.KafkaProducerReportScheduled,
            LogLevel.Information,
            "New Report Scheduled event with a correlation id of {correlationId} was created.")]
        public static partial void LogKafkaProducerReportScheduled(this ILogger logger, string correlationId);

        [LoggerMessage(
            LinkAdminLoggingIds.KafkaProducerDataAcquisitionRequested,
            LogLevel.Information,
            "New Data Acquisition Requested event with a correlation id of {correlationId} was created.")]
        public static partial void LogKafkaProducerDataAcquisitionRequested(this ILogger logger, string correlationId);

        [LoggerMessage(
            LinkAdminLoggingIds.GatewayServiceUriException,
            LogLevel.Error,
            "An exception occured while accessing service {service} uri: {exception}")]
        public static partial void LogGatewayServiceUriException(this ILogger logger, string service, string exception);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkServiceRequestGenerated,
            LogLevel.Information,
            "A new request was generated for service {service}.")]
        public static partial void LogLinkServiceRequestGenerated(this ILogger logger, string service);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkServiceRequestException,
            LogLevel.Error,
            "An exception occured while making a request to service {service}: {exception}")]
        public static partial void LogLinkServiceRequestException(this ILogger logger, string service, string exception);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkServiceRequestWarning,
            LogLevel.Warning,
            "While making a request to service {service}: {exception}")]
        public static partial void LogLinkServiceRequestWarning(this ILogger logger, string service, string exception);

        [LoggerMessage(
            LinkAdminLoggingIds.LinkServiceRequestSuccess,
            LogLevel.Information,
            "Request to service {service} was successful.")]
        public static partial void LogLinkServiceRequestSuccess(this ILogger logger, string service);

        [LoggerMessage(
            LinkAdminLoggingIds.CacheException,
            LogLevel.Error,
            "An exception occured while attempting to access cache {cacheKey}: {message}.")]
        public static partial void LogCacheException(this ILogger logger, string cacheKey, string message);

    }
}
