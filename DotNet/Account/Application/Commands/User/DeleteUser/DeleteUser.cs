using Hl7.Fhir.Utility;
using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Link.Authorization.Infrastructure;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class DeleteUser : IDeleteUser
    {
        private readonly ILogger<CreateUser> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IAccountServiceMetrics _metrics;
        private readonly ICreateAuditEvent _createAuditEvent;
        private readonly ICacheService _cache;

        public DeleteUser(ILogger<CreateUser> logger, IUserRepository userRepository, ICacheService cache, IAccountServiceMetrics metrics, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, Guid userId, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, userId)];
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("ActivateUser:Execute", tagList);

            try
            {
                var user = await _userRepository.GetUserAsync(userId, cancellationToken: cancellationToken) ?? throw new ApplicationException($"User with id {userId} not found");

                if (user.IsDeleted)
                {
                    return true;
                }

                user.IsDeleted = true;

                if (requestor is not null)
                {
                    user.LastModifiedBy = requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }

                var result = await _userRepository.UpdateAsync(user, cancellationToken);

                if (!result)
                {
                    _logger.LogDeleteUserException(userId.ToString(), "Failed to delete user");
                    return false;
                }

                //generate tags for telemetry                
                foreach (var claim in user.Claims.Where(x => x.ClaimType == LinkAuthorizationConstants.LinkSystemClaims.Facility))
                {
                    var tag = new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, claim.ClaimValue);
                    tagList.Add(tag);
                    activity?.AddTag(tag.Key, tag.Value);
                }
                _metrics.IncrementAccountDeletedCounter(tagList);
                _logger.LogDeleteUser(userId.ToString(), requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Unknown");

                //generate audit event
                var auditMessage = new AuditEventMessage
                {                    
                    Action = AuditEventType.Delete,
                    EventDate = DateTime.UtcNow,
                    UserId = user.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkUser).Name,
                    PropertyChanges = new List<PropertyChangeModel>([
                        new PropertyChangeModel("IsDeleted", "False", "True")
                    ]),
                    Notes = $"User ({user.Id}) deleted by '{user.LastModifiedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                var userKey = "user:" + user.Email;

                try
                {
                    _cache.Remove(userKey);
                }
                catch (Exception ex)
                {
                    _logger.LogCacheException(userKey, ex.Message);
                }


                return result;
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
