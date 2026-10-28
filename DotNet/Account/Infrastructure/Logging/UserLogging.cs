using LantanaGroup.Link.Account.Application.Models.User;
using static LantanaGroup.Link.Account.Settings.AccountConstants;

namespace LantanaGroup.Link.Account.Infrastructure.Logging
{
    public static partial class UserLogging
    {
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

        [LoggerMessage(
            AccountLoggingIds.UpdateUser,
            LogLevel.Information,
            "User {userId} was updated by {requestor}.")]
        public static partial void LogUpdateUser(this ILogger logger, string userId, string requestor);

        [LoggerMessage(
            AccountLoggingIds.UpdateUserException,
            LogLevel.Error,
            "An exception occured while updating user {userId}: {message}")]
        public static partial void LogUpdateUserException(this ILogger logger, string userId, string message);

        [LoggerMessage(
            AccountLoggingIds.DeactivateUser,
            LogLevel.Information,
            "User {userId} was deactivated by {requestor}.")]
        public static partial void LogDeactivateUser(this ILogger logger, string userId, string requestor);

        [LoggerMessage(
            AccountLoggingIds.DeactivateUserException,
            LogLevel.Error,
            "An exception occured while deactivating user {userId}: {message}")]
        public static partial void LogDeactivateUserException(this ILogger logger, string userId, string message);

        [LoggerMessage(
            AccountLoggingIds.ActivateUser,
            LogLevel.Information,
            "User {userId} was activated by {requestor}.")]
        public static partial void LogActivateUser(this ILogger logger, string userId, string requestor);

        [LoggerMessage(
            AccountLoggingIds.ActivateUserException,
            LogLevel.Error,
            "An exception occured while activating user {userId}: {message}")]
        public static partial void LogActivateUserException(this ILogger logger, string userId, string message);

        [LoggerMessage(
            AccountLoggingIds.FindUser,
            LogLevel.Information,
            "A request to find user {userId} by {requestor} was successful.")]
        public static partial void LogFindUser(this ILogger logger, string userId, string requestor);

        [LoggerMessage(
            AccountLoggingIds.FindUserException,
            LogLevel.Error,
            "An exception occured while attempting to find user {userId}: {message}")]
        public static partial void LogFindUserException(this ILogger logger, string userId, string message);

        [LoggerMessage(
            AccountLoggingIds.FindUsers,
            LogLevel.Information,
            "A request to find users by {requestor} was successful.")]
        public static partial void LogFindUsers(this ILogger logger, string requestor);

        [LoggerMessage(
            AccountLoggingIds.FindUsersException,
            LogLevel.Error,
            "An exception occured while attempting to find users: {message}")]
        public static partial void LogFindUsersException(this ILogger logger, string message);

        [LoggerMessage(
            AccountLoggingIds.UserClaimAssignment,
            LogLevel.Information,
            "User {userId} was assigned claim {claimType} with value {claimValue} by {requestor}.")]
        public static partial void LogUserClaimAssignment(this ILogger logger, string userId, string claimType, string claimValue, string requestor);

        [LoggerMessage(
            AccountLoggingIds.UserClaimAssignmentException,
            LogLevel.Error,
            "An exception occured while assigning claim {claimType} with value {claimValue} to user {userId}: {message}")]
        public static partial void LogUserClaimAssignmentException(this ILogger logger, string userId, string claimType, string claimValue, string message);

        [LoggerMessage(
            AccountLoggingIds.UserClaimRemoval,
            LogLevel.Information,
            "Claim {claimType} with value {claimValue} was removed from user {userId} by {requestor}.")]
        public static partial void LogUserClaimRemoval(this ILogger logger, string userId, string claimType, string claimValue, string requestor);

        [LoggerMessage(
            AccountLoggingIds.UserClaimRemovalException,
            LogLevel.Error,
            "An exception occured while removing claim {claimType} with value {claimValue} from user {userId}: {message}")]
        public static partial void LogUserClaimRemovalException(this ILogger logger, string userId, string claimType, string claimValue, string message);


        [LoggerMessage(
            AccountLoggingIds.DeleteUser,
            LogLevel.Information,
            "User {userId} was deleted by {requestor}.")]
        public static partial void LogDeleteUser(this ILogger logger, string userId, string requestor);

        [LoggerMessage(
            AccountLoggingIds.DeleteUserException,
            LogLevel.Error,
            "An exception occured while deleting user {userId}: {message}")]
        public static partial void LogDeleteUserException(this ILogger logger, string userId, string message);

        [LoggerMessage(
            AccountLoggingIds.UserRecovery,
            LogLevel.Information,
            "User {userId} was recovered by {requestor}.")]
        public static partial void LogUserRecovery(this ILogger logger, string userId, string requestor);

        [LoggerMessage(
            AccountLoggingIds.UserRecoveryException,
            LogLevel.Error,
            "An exception occured while recovering user {userId}: {message}")]
        public static partial void LogUserRecoveryException(this ILogger logger, string userId, string message);

        [LoggerMessage(
            AccountLoggingIds.SearchUsers,
            LogLevel.Information,
            "A request to search users by {requestor} was successful.")]
        public static partial void LogSearchUsers(this ILogger logger, string requestor, [LogProperties] UserSearchFilterRecord filters);

        [LoggerMessage(
            AccountLoggingIds.SearchUsersException,
            LogLevel.Error,
            "An exception occured while attempting to search users: {message}")]
        public static partial void LogSearchUsersException(this ILogger logger, string message, [LogProperties] UserSearchFilterRecord filters);

    }
}
