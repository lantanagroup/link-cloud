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

    }
}
