
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints
{
    public class AuthEndpoints : IApi
    {
        private readonly ILogger<AuthEndpoints> _logger;

        public AuthEndpoints(ILogger<AuthEndpoints> logger)
        {
            _logger = logger;
        }

        public void RegisterEndpoints(WebApplication app)
        {
            var authEndpoints = app.MapGroup("/")
                .WithOpenApi(x => new OpenApiOperation(x)
                {                    
                    Tags = new List<OpenApiTag> { new() { Name = "Auth" } }
                });

            authEndpoints.MapGet("/login", Login)                
                .AllowAnonymous()
                .Produces(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                 {
                     Summary = "Login to Link",
                     Description = "Initiates the login process for link"                    
                 });

            authEndpoints.MapGet("/user", GetUser)
                .RequireAuthorization("AuthenticatedUser")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get user information",
                    Description = "Retrieves information about the current logged in user"
                });

            authEndpoints.MapGet("/logout", Logout)
                .RequireAuthorization()               
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Logout of Link",
                    Description = "Initiates the logout process for link"
                });

            _logger.LogInformation("Auth Endpoints Registered");

        }

        public IResult Login()
        {
            //TODO: DI authentication schema options from settings
            return Results.Challenge(authenticationSchemes: ["link_oauth2"]);
        }

        public async Task<IResult> GetUser()
        {
            return Results.Ok(new { Message = "Get User" });
        }

        public async Task<IResult> Logout()
        {            
            return Results.Ok(new { Message = "Logout" });
        }
          
        
    }
}
