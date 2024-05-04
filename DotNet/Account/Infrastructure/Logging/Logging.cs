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

        [LoggerMessage(
            AccountLoggingIds.UserCreated,
            LogLevel.Information,
            "User {userId} was created by {requestor}.")]
        public static partial void LogUserCreated(this ILogger logger, string userId, string requestor);

        [LoggerMessage(
            AccountLoggingIds.UserCreationException,
            LogLevel.Error,
            "An exception occured while creating user: {message}")]
        public static partial void LogUserCreationException(this ILogger logger, string message);

        [LoggerMessage(
            AccountLoggingIds.UserAddedToRole,
            LogLevel.Information,
            "User {userId} was added to role {role} by {requestor}.")]
        public static partial void LogUserAddedToRole(this ILogger logger, string userId, string role, string requestor);

        [LoggerMessage(
            AccountLoggingIds.UserRemovedFromRole,
            LogLevel.Information,
            "User {userId} was removed from role {role} by {requestor}.")]
        public static partial void LogUserRemovedFromRole(this ILogger logger, string userId, string role, string requestor);

        [LoggerMessage(
            AccountLoggingIds.UserRoleAssignmentException,
            LogLevel.Error,
            "An exception occured while assigning user {userId} to role {role}: {message}")]
        public static partial void LogUserRoleAssignmentException(this ILogger logger, string userId, string role, string message);

        [LoggerMessage(
            AccountLoggingIds.UserRoleRemovalException,
            LogLevel.Error,
            "An exception occured while removing user {userId} from role {role}: {message}")]
        public static partial void LogUserRoleRemovalException(this ILogger logger, string userId, string role, string message);
    }
}
