﻿using Hl7.FhirPath.Sprache;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces.Services;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

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
                .RequireAuthorization("AuthenticatedUser")               
                .Produces<object>(StatusCodes.Status200OK)
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
            return Results.Challenge(
                properties: new AuthenticationProperties { 
                    RedirectUri = "/" 
                },                
                authenticationSchemes: [ _authSchemaOptions.Value.DefaultChallengeScheme ]);
        }

        public IResult GetUser(HttpContext context)
        {
            var user = context.User;
            return Results.Ok(user.Claims.Select(x => new { x.Type, x.Value }).ToList());
        }

        public IResult Logout(HttpContext context)
        {
            context.SignOutAsync(LinkAdminConstants.AuthenticationSchemes.Cookie);
            return Results.Ok(new { Message = "Successfully logged out of Link Admin!" });
        }
          
        
    }
}
