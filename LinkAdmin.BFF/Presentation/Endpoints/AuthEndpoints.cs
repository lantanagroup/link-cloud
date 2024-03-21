
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints
{
    public static class AuthEndpoints
    {
        public static void RegisterAuthEndpoints(this WebApplication app)
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

        }

        static async Task<IResult> Login()
        {            
            return Results.Ok(new { Message = "Login" });
        }

        static async Task<IResult> GetUser()
        {
            return Results.Ok(new { Message = "Get User" });
        }

        static async Task<IResult> Logout()
        {            
            return Results.Ok(new { Message = "Logout" });
        }
          
        
    }
}
