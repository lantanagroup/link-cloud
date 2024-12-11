using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Link.Authorization.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public class UpdateUser : IUpdateUser
    {
        private readonly ILogger<CreateUser> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IAccountServiceMetrics _metrics;
        private readonly ICreateAuditEvent _createAuditEvent;
        private readonly IRoleRepository _roleRepository;
        private readonly IOptions<CacheSettings> _cacheSettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public UpdateUser(ILogger<CreateUser> logger, IUserRepository userRepository, IAccountServiceMetrics metrics, ICreateAuditEvent createAuditEvent, IRoleRepository roleRepository, IOptions<CacheSettings> cacheSettings, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _cacheSettings = cacheSettings ?? throw new ArgumentNullException(nameof(cacheSettings));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, LinkUserModel model, CancellationToken cancellationToken = default)
        {
            //generate tags for telemetry
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, model.Id)];            
            //foreach (var facility in model.Facilities)
            //{
            //    var tag = new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facility);
            //}
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("UdpateUser:Execute", tagList);

            try
            { 
                var user = await _userRepository.GetUserAsync(model.Id, cancellationToken: cancellationToken) ?? throw new ApplicationException($"User with id {model.Id} not found");
                           
                List<PropertyChangeModel> changes = GetUserDiff(model, user);

                user.UserName = model.Username;
                user.Email = model.Email;
                user.FirstName = model.FirstName;
                user.MiddleName = model.MiddleName;
                user.LastName = model.LastName;

                if (requestor is not null)
                {
                    user.LastModifiedBy = requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }

                await _userRepository.UpdateAsync(user, cancellationToken);

                //update user roles
                var userRoles = user.UserRoles.Select(r => r.Role);
                var currentRoles = userRoles.Select(r => r.Name);
                var addedRoles = model.Roles.Except(currentRoles);
                var removedRoles = currentRoles.Except(model.Roles);

                if(addedRoles.Any())
                {
                    foreach(var role in addedRoles)
                    {
                        if (!string.IsNullOrEmpty(role))
                        {
                            var linkRole = await _roleRepository.GetRoleByNameAsync(role, cancellationToken: cancellationToken);

                            if(linkRole is null)
                            {
                                _logger.LogRoleNotFound(role);
                                continue;
                            }

                            await _userRepository.AddRoleAsync(user.Id, linkRole, cancellationToken);
                        }                        
                    }                                     
                }

                if(removedRoles.Any())
                {
                    foreach (var role in removedRoles)
                    {
                        if (!string.IsNullOrEmpty(role))
                        {
                            var linkRole = await _roleRepository.GetRoleByNameAsync(role, cancellationToken: cancellationToken);

                            if (linkRole is null)
                            {
                                _logger.LogRoleNotFound(role);
                                continue;
                            }

                            await _userRepository.RemoveRoleAsync(user.Id, linkRole, cancellationToken);
                        }
                    }
                }

                //capture role changes
                if (addedRoles.Any() || removedRoles.Any())
                {
                    changes.Add(new PropertyChangeModel("Roles", string.Join(",", currentRoles), string.Join(",", model.Roles)));
                }

                //update user claims
                var currentClaims = user.Claims.Select(c => c.ClaimValue);
                var addedClaims = model.UserClaims.Except(currentClaims);
                var removedClaims = currentClaims.Except(model.UserClaims);

                if(addedClaims.Any())
                {
                    foreach(var claim in addedClaims)
                    {
                        if (!string.IsNullOrEmpty(claim))
                        {
                            Claim userClaim = new(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim);
                            var result = await _userRepository.AddClaimToUserAsync(user.Id, userClaim, cancellationToken);

                            if (!result)
                            {
                                _logger.LogUserClaimAssignmentException(user.Id.ToString(), userClaim.Type, userClaim.Value, "Failed to add claim while updating user.");
                            }

                            _logger.LogUserClaimAssignment(user.Id.ToString(), userClaim.Type, userClaim.Value, user.CreatedBy ?? "Unknown");
                        }

                    }
                }

                if(removedClaims.Any())
                {
                    foreach(var claim in removedClaims)
                    {
                        if (!string.IsNullOrEmpty(claim))
                        {
                            Claim userClaim = new(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim);
                            var result = await _userRepository.RemoveClaimFromUserAsync(user.Id, userClaim, cancellationToken);

                            if (!result)
                            {
                                _logger.LogUserClaimRemovalException(user.Id.ToString(), userClaim.Type, userClaim.Value, "Failed to remove claim while updating user.");
                            }

                            _logger.LogUserClaimRemoval(user.Id.ToString(), userClaim.Type, userClaim.Value, user.CreatedBy ?? "Unknown");
                        }
                    }
                }

                //capture claim changes
                if (addedClaims.Any() || removedClaims.Any())
                {
                    changes.Add(new PropertyChangeModel("Claims", string.Join(",", currentClaims), string.Join(",", model.UserClaims)));
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
                    Notes = $"User ({user.Id}) updated by '{user.CreatedBy}'."
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
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }

        private List<PropertyChangeModel> GetUserDiff(LinkUserModel model, LinkUser user)
        {
            List<PropertyChangeModel> changes = [];

            if(user.UserName != model.Username)
            {
                changes.Add(new PropertyChangeModel("UserName", user.UserName ?? string.Empty, model.Username));
            }

            if(user.Email != model.Email)
            {
                changes.Add(new PropertyChangeModel("Email", user.Email ?? string.Empty, model.Email));
            }

            if(user.FirstName != model.FirstName)
            {
                changes.Add(new PropertyChangeModel("FirstName", user.FirstName, model.FirstName));
            }

            if(user.MiddleName != model.MiddleName)
            {
                changes.Add(new PropertyChangeModel("MiddleName", user.MiddleName ?? string.Empty, model.MiddleName ?? string.Empty));
            }

            if(user.LastName != model.LastName)
            {
                changes.Add(new PropertyChangeModel("LastName", user.LastName, model.LastName));
            }

            return changes;
        }
    }
}
