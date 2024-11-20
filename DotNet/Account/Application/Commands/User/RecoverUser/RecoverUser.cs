using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;
using System.Security.Claims;
using Link.Authorization.Infrastructure;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class RecoverUser : IRecoverUser
    {
        private readonly ILogger<DeactivateUser> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IAccountServiceMetrics _metrics;
        private readonly ILinkUserModelFactory  _linkUserModelFactory;
        private readonly ICreateAuditEvent _createAuditEvent;

        public RecoverUser(ILogger<DeactivateUser> logger, IUserRepository userRepository, IAccountServiceMetrics metrics, ILinkUserModelFactory linkUserModelFactory, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _linkUserModelFactory = linkUserModelFactory ?? throw new ArgumentNullException(nameof(linkUserModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
        }

        public async Task<LinkUserModel> Execute(ClaimsPrincipal? requestor, Guid userId, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, userId)];
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("RecoverUser:Execute", tagList);

            try
            {
                var user = await _userRepository.GetUserAsync(userId, cancellationToken: cancellationToken) ?? throw new ApplicationException($"User with id {userId} not found");

                if (!user.IsDeleted)
                {
                    throw new InvalidOperationException($"User with id {userId} has not been deleted");
                }

                user.IsDeleted = false;

                if (requestor is not null)
                {
                    user.LastModifiedBy = requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }                

                var result = await _userRepository.UpdateAsync(user, cancellationToken);

                if (!result)
                {
                    throw new ApplicationException($"Unable to recover user.");
                }

                //generate tags for telemetry                
                foreach (var claim in user.Claims.Where(x => x.ClaimType == LinkAuthorizationConstants.LinkSystemClaims.Facility))
                {
                    var tag = new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, claim.ClaimValue);
                    tagList.Add(tag);
                    activity?.AddTag(tag.Key, tag.Value);
                }
                _metrics.IncrementAccountRestoredCounter(tagList);
                _logger.LogUserRecovery(userId.ToString(), user.LastModifiedBy ?? "Unknown");

                //generate audit event
                var auditMessage = new AuditEventMessage
                {                    
                    Action = AuditEventType.Update,
                    EventDate = DateTime.UtcNow,
                    UserId = user.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkUser).Name,
                    PropertyChanges = new List<PropertyChangeModel>([
                        new PropertyChangeModel("IsDeleted", "True", "False")
                    ]),
                    Notes = $"User ({user.Id}) recovered by '{user.LastModifiedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                return _linkUserModelFactory.Create(user);

            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }

        }
    }
}
