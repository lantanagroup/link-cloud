using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role
{
    public class DeleteRole : IDeleteRole
    {
        private readonly ILogger<DeleteRole> _logger;
        private readonly IRoleRepository _roleRepository;
        private readonly ILinkRoleModelFactory _roleModelFactory;
        private readonly ICreateAuditEvent  _createAuditEvent;

        public DeleteRole(ILogger<DeleteRole> logger, IRoleRepository roleRepository, ILinkRoleModelFactory roleModelFactory, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _roleModelFactory = roleModelFactory ?? throw new ArgumentNullException(nameof(roleModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, Guid roleId, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("DeleteRole:Execute");
            try
            { 
                var role = await _roleRepository.GetRoleAsync(roleId, cancellationToken: cancellationToken) ?? throw new ApplicationException($"Role with id {roleId} not found");
                activity?.AddTag(DiagnosticNames.Role, role.Name);

                if (requestor is not null)
                {
                    role.LastModifiedBy = requestor?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                }

                var result = await _roleRepository.DeleteAsync(role.Id, cancellationToken);

                if (!result)
                {
                    throw new ApplicationException($"Failed to delete role {role.Name}");
                }

                _logger.LogRoleDeleted(role.Name ?? string.Empty, role.LastModifiedBy ?? string.Empty, _roleModelFactory.Create(role));

                //generate audit event
                var auditMessage = new AuditEventMessage
                {
                    Action = AuditEventType.Delete,
                    EventDate = DateTime.UtcNow,
                    UserId = role.LastModifiedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkRole).Name,
                    Notes = $"Role ({role.Id}) deleted by '{role.LastModifiedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

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
