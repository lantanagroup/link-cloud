using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Notification.Infrastructure.Middleware
{
    public class LoggingScopeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingScopeMiddleware> _logger;

        public LoggingScopeMiddleware(RequestDelegate next, ILogger<LoggingScopeMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.User.Identity is { IsAuthenticated: true })
            {
                var user = context.User;              
                var userId = user.Claims.First(c => c.Type == "sub")?.Value;
                using (_logger.BeginScope("UserId:{userId}", userId))
                {
                    await _next(context);
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}
