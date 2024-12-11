using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
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

namespace LantanaGroup.Link.Account.Application.Commands.Role
{
    public class UpdateRoleClaims : IUpdateRoleClaims
    {
        private readonly ILogger<UpdateRoleClaims> _logger;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILinkRoleModelFactory _roleModelFactory;
        private readonly ICreateAuditEvent _createAuditEvent;
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public UpdateRoleClaims(ILogger<UpdateRoleClaims> logger, IRoleRepository roleRepository, IUserRepository userRepository, ILinkRoleModelFactory roleModelFactory, ICreateAuditEvent createAuditEvent, IOptions<CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _roleModelFactory = roleModelFactory ?? throw new ArgumentNullException(nameof(roleModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, Guid roleId, List<string> claims, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("UpdateClaims:Execute");

            try
            {
                var role = await _roleRepository.GetRoleAsync(roleId, cancellationToken: cancellationToken);

                if (role is null)
                {
                    _logger.LogRoleClaimAssignmentException(roleId.ToString(), string.Join(",", claims), "Role not found");
                    return false;
                }

                if (requestor is not null)
                {
                    role.LastModifiedBy = requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }

                var currentClaims = await _roleRepository.GetClaimsAsync(role.Id, cancellationToken);
                var addedClaims = claims.Except(currentClaims.Select(c => c.Value));
                var removedClaims = currentClaims.Select(c => c.Value).Except(claims);

                foreach (var claim in addedClaims)
                {
                    var newClaim = new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim);
                    var outcome = await _roleRepository.AddClaimAsync(role.Id, newClaim, cancellationToken);

                    if (outcome)
                    { 
                        _logger.LogRoleClaimAssignment(role.Id.ToString(), newClaim.Type, newClaim.Value, requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Unknown");
                    }
                }

                foreach (var claim in removedClaims)
                {
                    var roleClaim = currentClaims.FirstOrDefault(c => c.Value == claim);
                    if (roleClaim is not null)
                    {
                        var outcome = await _roleRepository.RemoveClaimAsync(role.Id, roleClaim, cancellationToken);

                        if (outcome)
                        {
                            _logger.LogRoleClaimAssignment(role.Id.ToString(), roleClaim.Type, roleClaim.Value, requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Unknown");
                        }
                    }
                }

                //Capture changes
                List<PropertyChangeModel> changes = [];
                if(addedClaims.Any() || removedClaims.Any())
                {
                    changes.Add(new PropertyChangeModel("Claims", string.Join(",", currentClaims), string.Join(",", claims)));
                }             

                _logger.LogRoleUpdated(role.Name ?? string.Empty, role.LastModifiedBy ?? string.Empty, _roleModelFactory.Create(role));

                //generate audit event
                var auditMessage = new AuditEventMessage
                {
                    Action = AuditEventType.Update,
                    EventDate = DateTime.UtcNow,
                    UserId = role.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkRole).Name,
                    PropertyChanges = changes,
                    Notes = $"Role ({role.Id}) updated by '{role.LastModifiedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                //clear user cache for any user with the role that has changed
                if (_cacheSettings.Value.Enabled && !string.IsNullOrEmpty(role.Name))
                {
                    var users = await _userRepository.GetRoleUsersAsync(role.Name, cancellationToken);

                    if (users.Any())
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

                        foreach (var user in users)
                        {
                            var userKey = $"user:{user.Email}";
                            await _cache.RemoveAsync(userKey, cancellationToken);
                        }
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
