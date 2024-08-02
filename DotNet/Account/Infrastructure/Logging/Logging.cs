using LantanaGroup.Link.Shared.Application.Models.Kafka;
using static LantanaGroup.Link.Account.Settings.AccountConstants;

namespace LantanaGroup.Link.Account.Infrastructure.Logging
{
    public static partial class Logging
    {
        [LoggerMessage(
            AccountLoggingIds.ApiRegistered,
            LogLevel.Information,
            "API {api} registered.")]
        public static partial void LogApiRegistration(this ILogger logger, string api);

        //create logger message for audit event created
        [LoggerMessage(
            AccountLoggingIds.AuditEventCreated,
            LogLevel.Information,
            "Audit event created.")]
        public static partial void LogAuditEventCreated(this ILogger logger, [LogProperties]AuditEventMessage auditEvent);

        //create logger message for audit event creation exception
        [LoggerMessage(
            AccountLoggingIds.AuditEventCreationException,
            LogLevel.Error,
            "An exception occured while trying to generate an audit event: {message}.")]
        public static partial void LogAuditEventCreationException(this ILogger logger, string message, [LogProperties] AuditEventMessage auditEvent);

        [LoggerMessage(
            AccountLoggingIds.CacheException,
            LogLevel.Error,
            "An exception occured while attempting to access cache {cacheKey}: {message}.")]
        public static partial void LogCacheException(this ILogger logger, string cacheKey, string message);
       
    }
}
