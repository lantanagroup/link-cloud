using System.Diagnostics;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Link.Authorization.Infrastructure;
using Link.Authorization.Policies;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints
{
    public class BearerServiceEndpoints(
        ILogger<BearerServiceEndpoints> logger,
        IOptions<LinkTokenServiceSettings> tokenServiceConfig,
        ICreateLinkBearerToken createLinkBearerToken,
        IRefreshSigningKey refreshSigningKey)
        : IApi
    {
        private readonly ILogger<BearerServiceEndpoints> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IOptions<LinkTokenServiceSettings> _tokenServiceconfig = tokenServiceConfig ?? throw new ArgumentNullException(nameof(tokenServiceConfig));
        private readonly ICreateLinkBearerToken _createLinkBearerToken = createLinkBearerToken ?? throw new ArgumentNullException(nameof(createLinkBearerToken));
        private readonly IRefreshSigningKey _refreshSigningKey = refreshSigningKey ?? throw new ArgumentNullException(nameof(refreshSigningKey));

        public void RegisterEndpoints(WebApplication app)
        {
            var tokenEndpoints = app.MapGroup("/api/auth")
                .RequireAuthorization([LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName])
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Link Bearer Services" } }
                });

            tokenEndpoints.MapGet("/token", (Delegate)CreateToken)
                .Produces<string>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Generate link bearer token.",
                    Description = "Generates a bearer token for the current user to use with Link Services."
                });

            tokenEndpoints.MapGet("/refresh-key", (Delegate)RefreshKey)
                .RequireAuthorization([PolicyNames.IsLinkAdmin])
                .Produces<KeyRefreshedResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Refresh link bearer token signing key.",
                    Description = "Refreshes the signing key for link bearer tokens."
                });

            _logger.LogApiRegistration(nameof(BearerServiceEndpoints));
        }

        public async Task<IResult> CreateToken(HttpContext context)
        {
            if (!_tokenServiceconfig.Value.EnableTokenGenerationEndpoint)
            {
                return Results.BadRequest("Token generation is disabled.");
            }

            try
            {
                var user = context.User;

                var token = await _createLinkBearerToken.ExecuteAsync(user, _tokenServiceconfig.Value.TokenLifespan, context.RequestAborted);

                _logger.LogLinkAdminTokenGenerated(DateTime.UtcNow, user.Claims.First(c => c.Type == "sub")?.Value ?? "subject missing");

                return Results.Ok(token);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                _logger.LogLinkAdminTokenGenerationException(ex.Message);
                throw;
            }

        }

        public async Task<IResult> RefreshKey(HttpContext context)
        {
            try
            {
                var result = await _refreshSigningKey.ExecuteAsync(context.User, context.RequestAborted);
                _logger.LogLinkAdminTokenKeyRefreshed(DateTime.UtcNow);

                return Results.Ok(new KeyRefreshedResponse
                {
                    Message = result ? "The signing key for link bearer services was refreshed successfully." :
                    "Failed to refresh the signing key for link bearer services."
                });
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.AddException(ex);
                _logger.LogLinkAdminTokenGenerationException(ex.Message);
                throw;
            }

        }


    }
}
