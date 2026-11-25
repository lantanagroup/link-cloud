using LantanaGroup.Link.Account.Application.Interfaces.Presentation;
using LantanaGroup.Link.Account.Application.Models;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Presentation.Endpoints.User.Handlers;
using LantanaGroup.Link.Shared.Application.Filters;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Link.Authorization.Permissions;
using Link.Authorization.Policies;
using Microsoft.OpenApi;

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
                .RequireAuthorization([PolicyNames.IsLinkAdmin])
                .WithTags("User");

            #region Queries

            userEndpoints.MapGet("/user/{id}", GetUser.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanViewAccounts)])
                .Produces<LinkUserModel>(StatusCodes.Status200OK)
                .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Get user by id")
                .WithDescription("Retrieves information about a user with the id provided");

            userEndpoints.MapGet("/user/email/{email}", GetUserByEmail.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanViewAccounts)])
                .Produces<LinkUserModel>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Get user by id")
                .WithDescription("Retrieves information about a user with the email address provided");

            userEndpoints.MapGet("/user/facility/{id}", GetUsersByFacility.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanViewAccounts)])
                .Produces<GroupedUserModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Get facility users")
                .WithDescription("Retrieves users associated with the facility id provided");

            userEndpoints.MapGet("/user/role/{id}", GetUsersByRole.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanViewAccounts)])
                .Produces<GroupedUserModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Get users in role")
                .WithDescription("Retrieves users assigned to the role provided");

            userEndpoints.MapGet("/users", SearchForUsers.Handle)
               //.RequireAuthorization([nameof(LinkSystemPermissions.CanViewAccounts)])
               .Produces<GroupedUserModel>(StatusCodes.Status200OK)
               .Produces(StatusCodes.Status401Unauthorized)
               .Produces(StatusCodes.Status403Forbidden)
               .ProducesProblem(StatusCodes.Status500InternalServerError)
               .WithSummary("Search for users")
               .WithDescription("Searches for users with provided filters");

            //userEndpoints.MapGet("/users/facility/{id}", SearchForFacilityUsers.Handle)
            //   //.RequireAuthorization([nameof(LinkSystemPermissions.CanViewAccounts)])
            //   .Produces<GroupedUserModel>(StatusCodes.Status200OK)
            //   .Produces(StatusCodes.Status401Unauthorized)
            //   .Produces(StatusCodes.Status403Forbidden)
            //   .ProducesProblem(StatusCodes.Status500InternalServerError)
            //   .WithOpenApi(x => new OpenApiOperation(x)
            //   {
            //       Summary = "Search for users associated with a facility",
            //       Description = "Searches for facility users with provided filters"
            //   });

            #endregion

            #region Commands

            userEndpoints.MapPost("/user", CreateNewUser.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .AddEndpointFilter<ValidationFilter<LinkUserModel>>()
                .Produces<LinkUserModel>(StatusCodes.Status201Created)
                .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Create user")
                .WithDescription("Creates a new user");

            userEndpoints.MapPut("/user/{id}", UpdateExistingUser.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .AddEndpointFilter<ValidationFilter<LinkUserModel>>()
                .Produces(StatusCodes.Status204NoContent)
                .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Update user")
                .WithDescription("Updates an existing user");

            userEndpoints.MapDelete("/user/{id}", DeleteExistingUser.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Delete user")
                .WithDescription("Deletes an existing user from the system.");

            userEndpoints.MapPost("/user/{id}/recover", RecoverDeletedUser.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .Produces<LinkUserModel>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Recover a deleted user")
                .WithDescription("Recovers a deleted user and makes them active");

            userEndpoints.MapPost("/user/{id}/activate", ActivateExistingUser.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Activate a user")
                .WithDescription("Activates a user and makes them active");

            userEndpoints.MapPost("/user/{id}/deactivate", DeactivateExistingUser.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Deactivate a user")
                .WithDescription("Deactivates a user and makes them inactive");

            userEndpoints.MapPut("/user/{id}/claims", UpdateUserClaims.Handle)
                //.RequireAuthorization([nameof(LinkSystemPermissions.CanAdministerAccounts)])
                .AddEndpointFilter<ValidationFilter<LinkClaimsModel>>()
                .Produces<LinkUserModel>(StatusCodes.Status200OK)
                .Produces<ValidationFailureResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithSummary("Update user claims")
                .WithDescription("Updates the claims for an existing user");


            #endregion

            _logger.LogApiRegistration(nameof(UserEndpoints));
        }
    }
}
