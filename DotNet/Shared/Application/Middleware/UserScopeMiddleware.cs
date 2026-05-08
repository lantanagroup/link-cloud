using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Middleware
{
    public class UserScopeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserScopeMiddleware> _logger;

        public UserScopeMiddleware(RequestDelegate next, ILogger<UserScopeMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
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
