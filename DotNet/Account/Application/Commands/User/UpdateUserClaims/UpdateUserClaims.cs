using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class UpdateUserClaims : IUpdateUserClaims
    {
        private readonly ILogger<UpdateUserClaims> _logger;
        private readonly IUserRepository _userRepository;
        private readonly ILinkUserModelFactory _userModelFactory;
        private readonly ICreateAuditEvent _createAuditEvent;
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public UpdateUserClaims(ILogger<UpdateUserClaims> logger, IUserRepository userRepository, ILinkUserModelFactory userModelFactory, ICreateAuditEvent createAuditEvent, IOptions<CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _userModelFactory = userModelFactory ?? throw new ArgumentNullException(nameof(userModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, Guid userId, List<string> claims, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("UpdateClaims:Execute");

            try
            { 
                var user = await _userRepository.GetUserAsync(userId, cancellationToken: cancellationToken) ?? throw new ApplicationException($"User with id {userId} not found");

                var currentClaims = user.Claims;
                var addedClaims = claims.Except(currentClaims.Select(c => c.ClaimValue));
                var removedClaims = currentClaims.Select(c => c.ClaimValue).Except(claims);

                if (addedClaims.Any() && removedClaims.Any() && requestor is not null)
                {
                    user.LastModifiedBy = requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }

                foreach (var claim in addedClaims)
                {
                    if (!string.IsNullOrEmpty(claim))
                    {
                        var newClaim = new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim);
                        var outcome = await _userRepository.AddClaimToUserAsync(user.Id, newClaim, cancellationToken);
                        if(outcome)
                        {
                            _logger.LogUserClaimAssignment(user.Id.ToString(), newClaim.Type, newClaim.Value, requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Unknown");
                        }
                    }                    
                }

                foreach (var claim in removedClaims)
                {
                    var userClaim = currentClaims.FirstOrDefault(c => c.ClaimValue == claim);
                    if (userClaim is not null && !string.IsNullOrEmpty(userClaim.ClaimValue))
                    {
                        var removedClaim = new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, userClaim.ClaimValue);
                        var outcome = await _userRepository.RemoveClaimFromUserAsync(user.Id, removedClaim, cancellationToken);
                        if (outcome)
                        {
                            _logger.LogUserClaimRemoval(user.Id.ToString(), removedClaim.Type, removedClaim.Value, requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Unknown");
                        }
                    }
                }

                //Capture changes
                List<PropertyChangeModel> changes = [];
                if(addedClaims.Any() || removedClaims.Any())
                {
                    changes.Add(new PropertyChangeModel("Claims", string.Join(",", currentClaims), string.Join(",", claims)));
                }

                //generate audit event
                var auditMessage = new AuditEventMessage
                {
                    Action = AuditEventType.Update,
                    EventDate = DateTime.UtcNow,
                    UserId = user.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkUser).Name,
                    PropertyChanges = changes,
                    Notes = $"Role ({user.Id}) updated by '{user.LastModifiedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                //clear user cache if cache is enabled
                if (_cacheSettings.Value.Enabled)
                {
                    var userKey = $"user:{user.Email}";
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

                        await _cache.RemoveAsync(userKey, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCacheException(userKey, ex.Message);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                throw;
            }            
            
        }
    }
}
