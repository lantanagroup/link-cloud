using LantanaGroup.Link.Account.Application.Interfaces.Presentation;
using LantanaGroup.Link.Account.Application.Models;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Presentation.Endpoints.Claims.Handlers;
using Link.Authorization.Permissions;
using Link.Authorization.Policies;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Claims
{
    public class ClaimsEndpoints : IApi
    {
        private readonly ILogger<ClaimsEndpoints> _logger;

        public ClaimsEndpoints(ILogger<ClaimsEndpoints> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterEndpoints(WebApplication app)
        {
            var roleEndpoints = app.MapGroup("/api/account/")
                .RequireAuthorization([PolicyNames.IsLinkAdmin])
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Claim" } }
                });

            roleEndpoints.MapGet("/claims", GetClaims.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanViewAccounts)])
                .Produces<LinkClaimsModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get all claims",
                    Description = "Retrieves a list all claims that are assignable."
                });

            _logger.LogApiRegistration(nameof(ClaimsEndpoints));
        }
    }
}
