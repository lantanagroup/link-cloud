using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers;

public interface IExceptionLogger<T>
{
    void Handle(Exception ex, string message,
                LogLevel level = LogLevel.Error,
                string? facilityId = null,
                object? context = null);
}

public class ExceptionLogger<T> : IExceptionLogger<T>
{
    private readonly ILogger<T> _logger;

    public ExceptionLogger(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void Handle(Exception ex, string message,
                       LogLevel level = LogLevel.Error,
                       string? facilityId = null,
                       object? context = null)
    {
        try
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error);
            Activity.Current?.AddException(ex);

            _logger.Log(level, ex, "{Caller}: {Message}. FacilityId: {FacilityId} (Context: {Context})",
                typeof(T).Name,
                message.SanitizeForLog(),
                facilityId?.SanitizeForLog() ?? "N/A",
                context.SanitizeForLog() ?? "None");
        }
        catch (Exception loggingEx)
        {
            // Failsafe – never let logging itself crash the app
            _logger.LogCritical(loggingEx, "{Caller}: Failed to log exception", typeof(T).Name);
        }
    }
}