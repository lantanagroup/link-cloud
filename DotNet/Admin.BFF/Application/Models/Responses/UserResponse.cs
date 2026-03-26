using Link.Authorization.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Responses
{
    public record UserResponse
    {
        /// <summary>
        /// The user's first name
        /// </summary>
        /// <example>Jane</example>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// The user's last name
        /// </summary>
        /// <example>Doe</example>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// The user's email address
        /// </summary>
        /// <example>jane.doe@gmail.com</example>
        public string Email{ get; set; } = string.Empty;

        /// <summary>
        /// The roles assigned to the user
        /// </summary>
        /// <example>["LinkUser"]</example>
        public List<string> Roles { get; set; } = [];

        /// <summary>
        /// The permissions assigned to the user
        /// </summary>
        /// <example>["CanViewLogs"]</example>
        public List<string> Permissions { get; set; } = [];


        public static UserResponse FromClaims(ClaimsIdentity identity)
        {
            var claims = identity.Claims;
            var user = new UserResponse
            {
                FirstName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value ?? string.Empty,
                LastName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value ?? string.Empty,
                Email = claims.FirstOrDefault(c => c.Type == LinkAuthorizationConstants.LinkSystemClaims.Email)?.Value ?? string.Empty,
                Roles = claims.Where(c => c.Type == LinkAuthorizationConstants.LinkSystemClaims.Role).Select(c => c.Value).ToList(),
                Permissions = claims.Where(c => c.Type == LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions).Select(c => c.Value).ToList()
            };

            return user;
        }

    }
}
