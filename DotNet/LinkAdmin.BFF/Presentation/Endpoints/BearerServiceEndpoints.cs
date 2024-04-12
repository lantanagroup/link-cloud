﻿using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints
{
    public class BearerServiceEndpoints : IApi
    {
        private readonly ILogger<BearerServiceEndpoints> _logger;
        private readonly ICreateLinkBearerToken _createLinkBearerToken;
        private readonly IRefreshSigningKey _refreshSigningKey;

        public BearerServiceEndpoints(ILogger<BearerServiceEndpoints> logger, ICreateLinkBearerToken createLinkBearerToken, IRefreshSigningKey refreshSigningKey)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _createLinkBearerToken = createLinkBearerToken ?? throw new ArgumentNullException(nameof(createLinkBearerToken));
            _refreshSigningKey = refreshSigningKey ?? throw new ArgumentNullException(nameof(refreshSigningKey));
        }

        public void RegisterEndpoints(WebApplication app)
        {
            var tokenEndpoints = app.MapGroup("/auth")
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Link Bearer Services" } }
                });

            tokenEndpoints.MapGet("/token", (Delegate)CreateToken)
                .RequireAuthorization("AuthenticatedUser")
                .Produces<string>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Generate link bearer token.",
                    Description = "Generates a bearer token for the current user to use with Link Services."
                });

            tokenEndpoints.MapGet("/refresh-key", RefreshKey)
                .RequireAuthorization("AuthenticatedUser")
                .Produces<KeyRefreshedResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Refresh link bearer token signing key.",
                    Description = "Refreshes the signing key for link bearer tokens."
                });

            _logger.LogInformation("Bearer Service endpoints registered");
        }

        public async Task<IResult> CreateToken(HttpContext context)
        {
            try 
            {
                var user = context.User;
                var token = await _createLinkBearerToken.ExecuteAsync(user, 10);

                _logger.LogLinkAdminTokenGenerated(DateTime.UtcNow, user.Claims.First(c => c.Type == "sub")?.Value ?? "subject missing");

                return Results.Ok(token);
            }
            catch (Exception ex)
            {
                _logger.LogLinkAdminTokenGenerationException(ex.Message);
                throw;
            }
            
        }

        public async Task<IResult> RefreshKey()
        {
            try 
            {
                var result = await _refreshSigningKey.ExecuteAsync();
                _logger.LogLinkAdminTokenKeyRefreshed(DateTime.UtcNow);

                return Results.Ok(new KeyRefreshedResponse
                {
                    Message = result ? "The signing key for link bearer services was refreshed successfully." :
                    "Failed to refresh the signing key for link bearer services."
                });
            }
            catch (Exception ex)
            {
                _logger.LogLinkAdminTokenGenerationException(ex.Message);
                throw;
            }
            
        }


    }
}
