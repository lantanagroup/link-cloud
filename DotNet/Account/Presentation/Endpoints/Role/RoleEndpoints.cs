using LantanaGroup.Link.Account.Application.Interfaces.Presentation;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Presentation.Endpoints.Role.Handlers;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.Role
{
    public class RoleEndpoints : IApi
    {
        private readonly ILogger<RoleEndpoints> _logger;

        public RoleEndpoints(ILogger<RoleEndpoints> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterEndpoints(WebApplication app)
        {
            var roleEndpoints = app.MapGroup("/api/account/")
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Role" } }
                });

            #region Queries

            roleEndpoints.MapGet("/role/{id}", GetRoleById.Handle)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get role by id",
                    Description = "Retrieves information about a role with the id provided"
                });

            #endregion

            #region Commands

            #endregion

            _logger.LogApiRegistration(nameof(RoleEndpoints));
        }
    }
}
