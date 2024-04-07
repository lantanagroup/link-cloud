using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public class CreateLinkBearerToken : ICreateLinkBearerToken
    {
        private readonly ILogger<CreateLinkBearerToken> _logger;
        private readonly IDistributedCache _cache;
        private readonly ISecretManager _secretManager;

        public CreateLinkBearerToken(ILogger<CreateLinkBearerToken> logger, IDistributedCache cache, ISecretManager secretManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
        }

        public async Task<string> ExecuteAsync(ClaimsPrincipal user)
        {
            string? bearerKey = _cache.GetString("LinkBearerKey");

            if (bearerKey == null)
            {

                bearerKey = await _secretManager.GetSecretAsync("LinkBearer", CancellationToken.None);
                _cache.SetString("LinkBearerKey", bearerKey);
            }

            var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(bearerKey)), SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                                issuer: "LinkAdmin",
                                audience: "LinkSevices",                                
                                claims: user.Claims,
                                expires: DateTime.Now.AddMinutes(10),
                                signingCredentials: credentials
                            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }
    }
}
