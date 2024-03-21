
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
                .WithOpenApi(x => new OpenApiOperation(x)
                 {
                     Summary = "Login to Link",
                     Description = "Initiates the login process for link"                    
                 });

            authEndpoints.MapGet("/user", GetUser)
                .RequireAuthorization()
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get user information",
                    Description = "Retrieves information about the current logged in user"
                });

            authEndpoints.MapGet("/logout", Logout)
                .RequireAuthorization()               
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Logout of Link",
                    Description = "Initiates the logout process for link"
                });

            _logger.LogInformation("Auth Endpoints Registered");

        }

        public async Task<IResult> Login()
        {            
            return Results.Ok(new { Message = "Login" });
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
