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
using Link.Authorization.Infrastructure;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class RecoverUser : IRecoverUser
    {
        private readonly ILogger<DeactivateUser> _logger;
        private readonly UserManager<LinkUser> _userManager;
        private readonly IAccountServiceMetrics _metrics;

        public RecoverUser(ILogger<DeactivateUser> logger, UserManager<LinkUser> userManager, IAccountServiceMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public async Task<LinkUser> Execute(ClaimsPrincipal? requestor, string userId, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, userId)];
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("RecoverUser:Execute", tagList);

            try
            {
                var user = await _userManager.FindByIdAsync(userId) ?? throw new ApplicationException($"User with id {userId} not found");

                if (!user.IsDeleted)
                {
                    throw new InvalidOperationException($"User with id {userId} has not been deleted");
                }

                user.IsDeleted = false;

                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    throw new ApplicationException($"Unable to recover user: {result.Errors}");
                }

                //generate tags for telemetry                
                foreach (var claim in user.Claims.Where(x => x.ClaimType == LinkAuthorizationConstants.LinkSystemClaims.Facility))
                {
                    var tag = new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, claim.ClaimValue);
                    tagList.Add(tag);
                    activity?.AddTag(tag.Key, tag.Value);
                }
                _metrics.IncrementAccountRestoredCounter(tagList);
                _logger.LogUserRecovery(userId, requestor?.Claims.First(c => c.Type == "sub").Value ?? "Unknown");

                return user;

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogDeleteUserException(userId, ex.Message);
                throw;
            }

        }
    }
}
