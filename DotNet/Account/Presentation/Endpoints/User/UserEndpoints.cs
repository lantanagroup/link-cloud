using LantanaGroup.Link.Account.Application.Interfaces.Presentation;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.Account.Presentation.Endpoints.User
{
    public class UserEndpoints : IApi
    {
        private readonly ILogger<UserEndpoints> _logger;

        public UserEndpoints(ILogger<UserEndpoints> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterEndpoints(WebApplication app)
        {
            var userEndpoints = app.MapGroup("/api/account")
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Tags = new List<OpenApiTag> { new() { Name = "User" } }
                });

            #region Queries

            userEndpoints.MapGet("/user/{id}", GetUser.Handle)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get user by id",
                    Description = "Retrieves information about a user with the id provided"
                });

            userEndpoints.MapGet("/user/email/{email}", GetUserByEmail.Handle)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get user by id",
                    Description = "Retrieves information about a user with the email address provided"
                });

            userEndpoints.MapGet("/user/facility/{id}", GetUsersByFacility.Handle)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get facility users",
                    Description = "Retrieves users associated with the facility id provided"
                });

            userEndpoints.MapGet("/user/role/{id}", GetUsersByRole.Handle)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get users in role",
                    Description = "Retrieves users assigned to the role provided"
                });

            userEndpoints.MapGet("/users", SearchForUsers.Handle)
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status401Unauthorized)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesProblem(StatusCodes.Status500InternalServerError)
               .WithOpenApi(x => new OpenApiOperation(x)
               {
                   Summary = "Search for users",
                   Description = "Searches for users with provided filters"
               });

            userEndpoints.MapGet("/users/facility/{id}", SearchForFacilityUsers.Handle)
               .Produces(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status401Unauthorized)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesProblem(StatusCodes.Status500InternalServerError)
               .WithOpenApi(x => new OpenApiOperation(x)
               {
                   Summary = "Search for users associated with a facility",
                   Description = "Searches for facility users with provided filters"
               });

            #endregion

            #region Commands

            //userEndpoints.MapPost("/user", CreateUser.Handle)
            //    .RequireAuthorization("AuthenticatedUser")
            //    .AddEndpointFilter<ValidationFilter<User>>()
            //    .Produces(StatusCodes.Status201Created)
            //    .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
            //    .Produces(StatusCodes.Status401Unauthorized)
            //    .Produces(StatusCodes.Status403Forbidden)
            //    .ProducesProblem(StatusCodes.Status500InternalServerError)
            //    .WithOpenApi(x => new OpenApiOperation(x)
            //    {
            //        Summary = "Create user",
            //        Description = "Creates a new user"
            //    });

            #endregion

            _logger.LogApiRegistration(nameof(UserEndpoints));
        }
    }
}
