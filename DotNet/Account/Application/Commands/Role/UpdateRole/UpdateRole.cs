using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Link.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;


namespace LantanaGroup.Link.Account.Application.Commands.Role
{
    public class UpdateRole : IUpdateRole
    {
        private readonly ILogger<UpdateRole> _logger;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILinkRoleModelFactory _roleModelFactory;
        private readonly ICreateAuditEvent _createAuditEvent;
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ICacheService _cache;

        public UpdateRole(ILogger<UpdateRole> logger, IRoleRepository roleRepository, IUserRepository userRepository, ICacheService cache, ILinkRoleModelFactory roleModelFactory, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _roleModelFactory = roleModelFactory ?? throw new ArgumentNullException(nameof(roleModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache)); 
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, LinkRoleModel model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("UpdateRole:Execute", 
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.Role, model.Name),
                ]);

            try
            {
                var role = await _roleRepository.GetRoleAsync(model.Id, cancellationToken: cancellationToken) ?? throw new ApplicationException($"Role with id {model.Id} not found");

                if (role is null)
                {
                    _logger.LogRoleUpdateException(model.Name, "Role not found", model);
                    return false;
                }

                List<PropertyChangeModel> changes = GetRoleDiff(model, role);

                role.Name = model.Name;
                role.Description = model.Description;
                
                if(requestor is not null)
                {
                    role.LastModifiedBy = requestor.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }

                await _roleRepository.UpdateAsync(role, cancellationToken);

                //update role claims
                var currentClaims = await _roleRepository.GetClaimsAsync(role.Id, cancellationToken);
                var addedClaims = model.Claims.Except(currentClaims.Select(c => c.Value));
                var removedClaims = currentClaims.Select(c => c.Value).Except(model.Claims);

                if (addedClaims.Any())
                {
                    foreach (var claim in addedClaims)
                    {
                        await _roleRepository.AddClaimAsync(role.Id, new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim), cancellationToken);
                    }
                }

                if (removedClaims.Any())
                {
                    foreach (var claim in removedClaims)
                    {
                        var roleClaim = currentClaims.FirstOrDefault(c => c.Value == claim);
                        if (roleClaim is not null)
                        {
                            await _roleRepository.RemoveClaimAsync(role.Id, roleClaim, cancellationToken);
                        }
                    }
                }

                //capture claim changes
                if (addedClaims.Any() || removedClaims.Any())
                { 
                    changes.Add(new PropertyChangeModel("Claims", string.Join(",", currentClaims), string.Join(",", model.Claims)));
                }

                //generate audit event
                var auditMessage = new AuditEventMessage
                {
                    Action = AuditEventType.Update,
                    EventDate = DateTime.UtcNow,
                    UserId = role.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkRole).Name,
                    PropertyChanges = changes,
                    Notes = $"New role ({role.Id}) created by '{role.LastModifiedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                //clear user cache for any user with the role that has changed
                if (!string.IsNullOrEmpty(role.Name))
                {
                    var users = await _userRepository.GetRoleUsersAsync(role.Name, cancellationToken);

                    if (users.Any())
                    {
                        foreach (var user in users)
                        {
                            var userKey = $"user:{user.Email}";
                            _cache.Remove(userKey);
                        }
                    }                    
                }

                return true;
                
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }

        private List<PropertyChangeModel> GetRoleDiff(LinkRoleModel model, LinkRole role)
        {
            List<PropertyChangeModel> changes = [];

            if (model.Name != role.Name)
            {
                changes.Add(new PropertyChangeModel("Name", role.Name ?? string.Empty, model.Name));
            }

            if (model.Description != role.Description)
            {
                changes.Add(new PropertyChangeModel("Description", role.Description ?? string.Empty, model.Description));
            }

            return changes;
        }
    }
}
