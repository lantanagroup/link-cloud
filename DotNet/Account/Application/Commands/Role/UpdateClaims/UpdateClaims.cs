using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role.UpdateClaims
{
    public class UpdateClaims : IUpdateClaims
    {
        private readonly ILogger<UpdateClaims> _logger;
        private readonly IRoleRepository _roleRepository;
        private readonly ILinkRoleModelFactory _roleModelFactory;
        private readonly ICreateAuditEvent _createAuditEvent;

        public UpdateClaims(ILogger<UpdateClaims> logger, IRoleRepository roleRepository, ILinkRoleModelFactory roleModelFactory, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _roleModelFactory = roleModelFactory ?? throw new ArgumentNullException(nameof(roleModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, string roleId, List<string> claims, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("UpdateClaims:Execute");

            try
            { 
                var role = await _roleRepository.GetRoleAsync(roleId, cancellationToken: cancellationToken) ?? throw new ApplicationException($"Role with id {roleId} not found");

                var currentClaims = await _roleRepository.GetClaimsAsync(role.Id, cancellationToken);
                var addedClaims = claims.Except(currentClaims.Select(c => c.Value));
                var removedClaims = currentClaims.Select(c => c.Value).Except(claims);

                foreach (var claim in addedClaims)
                {
                    await _roleRepository.AddClaimAsync(role.Id, new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim), cancellationToken);
                }

                foreach (var claim in removedClaims)
                {
                    var roleClaim = currentClaims.FirstOrDefault(c => c.Value == claim);
                    if (roleClaim is not null)
                    {
                        await _roleRepository.RemoveClaimAsync(role.Id, roleClaim, cancellationToken);
                    }
                }

                //Capture changes
                List<PropertyChangeModel> changes = [];
                if(addedClaims.Any() || removedClaims.Any())
                {
                    changes.Add(new PropertyChangeModel("Claims", string.Join(",", currentClaims), string.Join(",", claims)));
                }

                if(requestor is not null)
                {
                    role.LastModifiedBy = requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }
         
                await _roleRepository.UpdateAsync(role, cancellationToken);                

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
