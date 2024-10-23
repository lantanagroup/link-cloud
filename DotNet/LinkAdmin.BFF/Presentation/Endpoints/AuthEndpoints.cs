using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints
{
    public class AuthEndpoints : IApi
    {
        private readonly ILogger<AuthEndpoints> _logger; 
        private readonly IOptions<AuthenticationSchemaConfig> _authSchemaOptions;

        public AuthEndpoints(ILogger<AuthEndpoints> logger, IOptions<AuthenticationSchemaConfig> oauthOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authSchemaOptions = oauthOptions ?? throw new ArgumentNullException(nameof(oauthOptions));
        }

        public void RegisterEndpoints(WebApplication app)
        {
            var authEndpoints = app.MapGroup("/api")
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
                .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
                .Produces<UserResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Get user information",
                    Description = "Retrieves information about the current logged in user"
                });

            authEndpoints.MapGet("/logout", Logout)
                .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)               
                .Produces<object>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status500InternalServerError)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Logout of Link",
                    Description = "Initiates the logout process for link"
                });

            _logger.LogApiRegistration(nameof(AuthEndpoints));
        }

        public IResult Login(HttpContext context)
        {            
            var RedirectLink = "/api/info";
            var referer = context.Request.Headers.Referer.ToString();
            referer = (referer.ToString().IndexOf('/') > 0) ? referer[..referer.LastIndexOf('/')] : referer;

            // if referer is not empty then set RedirectUri changes to referer + "/dashboard"
            if (!String.IsNullOrEmpty(referer))
            {
                RedirectLink = referer + "/dashboard";
            }

            return Results.Challenge(
                properties: new AuthenticationProperties
                {
                    RedirectUri = RedirectLink, 
                },
                authenticationSchemes: [_authSchemaOptions.Value.DefaultChallengeScheme]); 
        }

        public IResult GetUser(HttpContext context)
        {
            //get claims principal
            if (context.User.Identity is not ClaimsIdentity claimsIdentity)
            {
                return Results.Problem("No claims found in the current user identity", statusCode: StatusCodes.Status500InternalServerError);
            }            

            UserResponse user = UserResponse.FromClaims(claimsIdentity);

            return Results.Ok(user);
        }

        public IResult Logout(HttpContext context)
        {
            var referer = context.Request.Headers.Referer.ToString();
            referer = (referer.ToString().IndexOf('/') > 0) ? referer[..referer.LastIndexOf('/')] : referer;

            if (!string.IsNullOrEmpty(referer))
            {
                context.SignOutAsync(LinkAdminConstants.AuthenticationSchemes.Cookie);
                return Results.SignOut(properties: new AuthenticationProperties
                {
                    RedirectUri = referer
                },
                 authenticationSchemes: [LinkAdminConstants.AuthenticationSchemes.Cookie]);
            }
            else
            {
                context.SignOutAsync(LinkAdminConstants.AuthenticationSchemes.Cookie);
                return Results.SignOut(properties: new AuthenticationProperties
                {
                    RedirectUri = "/api/info"
                },
                 authenticationSchemes: [LinkAdminConstants.AuthenticationSchemes.Cookie]);
            }
        }
          
        
    }
}
