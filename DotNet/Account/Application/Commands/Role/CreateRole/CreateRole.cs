using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role
{
    public class CreateRole : ICreateRole
    {
        private readonly ILogger<CreateRole> _logger;
        private readonly RoleManager<LinkRole> _roleManager;
        private readonly ILinkRoleModelFactory _roleModelFactory;
        private readonly ICreateAuditEvent _createAuditEvent;

        public CreateRole(ILogger<CreateRole> logger, RoleManager<LinkRole> roleManager, ILinkRoleModelFactory roleModelFactory, ICreateAuditEvent createAuditEvent)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _roleModelFactory = roleModelFactory ?? throw new ArgumentNullException(nameof(roleModelFactory));
            _createAuditEvent = createAuditEvent ?? throw new ArgumentNullException(nameof(createAuditEvent));
        }

        public async Task<LinkRoleModel> Execute(ClaimsPrincipal? requestor, LinkRoleModel model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("CreateRole:Execute");

            try
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    throw new ArgumentException("A role name is required");
                }

                var role = new LinkRole
                {
                    Name = model.Name,
                    Description = model.Description                    
                };

                //add created by if requestor is provided
                if (requestor is not null)
                {
                    role.CreatedBy = requestor.Claims.First(c => c.Type == "sub").Value;
                }

                var result = await _roleManager.CreateAsync(role);

                if (!result.Succeeded)
                {
                    throw new ApplicationException($"Failed to create role {model.Name}");
                }

                //if claims were provided, add them
                if (model.Claims is not null)
                {
                    foreach (var claim in model.Claims)
                    {
                        result = await _roleManager.AddClaimAsync(role, 
                            new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim));

                        if (!result.Succeeded)
                        {
                            throw new ApplicationException($"Failed to add claim {claim} to role {model.Name}");
                        }
                    }
                }

                var roleModel = _roleModelFactory.Create(role);
                _logger.LogRoleCreated(roleModel.Name, role.CreatedBy ?? "Unknown", roleModel);

                //generate audit event
                var auditMessage = new AuditEventMessage
                {                    
                    Action = AuditEventType.Create,
                    EventDate = DateTime.UtcNow,
                    UserId = role.CreatedBy,
                    User = requestor?.Identity?.Name ?? string.Empty,
                    Resource = typeof(LinkRole).Name,
                    Notes = $"New role ({role.Id}) created by '{role.CreatedBy}'."
                };

                _ = Task.Run(() => _createAuditEvent.Execute(auditMessage, cancellationToken));

                return roleModel;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogRoleCreationException(model.Name, ex.Message, model);
                throw;
            }
        }
    }
}
