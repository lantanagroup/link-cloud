using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;
using static Link.Authorization.Infrastructure.LinkAuthorizationConstants;

namespace LantanaGroup.Link.Account.Application.Commands.User.DeactivateUser
{
    public class DeactivateUser : IDeactivateUser
    {
        private readonly ILogger<DeactivateUser> _logger;
        private readonly UserManager<LinkUser> _userManager;
        private readonly IAccountServiceMetrics _metrics;

        public DeactivateUser(ILogger<DeactivateUser> logger, UserManager<LinkUser> userManager, IAccountServiceMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, string userId, CancellationToken cancellationToken = default)
        {
            Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("DeactivateUser:Execute",
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.UserId, userId)    
                ]);

            try
            {
                var user = await _userManager.FindByIdAsync(userId) ?? throw new ApplicationException($"User with id {userId} not found");
                user.IsDeleted = true;

                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    throw new ApplicationException($"Unable to deactivate user: {result.Errors}");
                }

                //generate tags for telemetry
                List<KeyValuePair<string, object?>> tagList = [ new KeyValuePair<string, object?>(DiagnosticNames.UserId, userId)];
                foreach (var claim in user.Claims.Where(x => x.ClaimType == LinkSystemClaims.LinkPermissions))
                {
                    var tag = new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, claim.ClaimValue);
                    tagList.Add(tag);
                    activity?.AddTag(tag.Key, tag.Value);
                }
                _metrics.IncrementAccountDeactivatedCounter([..tagList]);
                _logger.LogDeactivateUser(user.Id, requestor?.Claims.First(c => c.Type == "sub").Value ?? "Unknown");

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                throw;
            }                  
        }
    }
}
