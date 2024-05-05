using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role.UpdateRole
{   
    public class UpdateRole : IUpdateRole
    {
        private readonly ILogger<UpdateRole> _logger;
        private readonly RoleManager<LinkRole> _roleManager;
        private readonly ILinkRoleModelFactory _roleModelFactory;

        public UpdateRole(ILogger<UpdateRole> logger, RoleManager<LinkRole> roleManager, ILinkRoleModelFactory roleModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _roleModelFactory = roleModelFactory ?? throw new ArgumentNullException(nameof(roleModelFactory));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, LinkRoleModel model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("UpdateRole:Execute", 
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.Role, model.Name),
                ]);

            try
            {
                var role = await _roleManager.FindByIdAsync(model.Id) ?? throw new ApplicationException($"Role with id {model.Id} not found");

                List<PropertyChangeModel> changes = GetRoleDiff(model, role);

                role.Name = model.Name;
                role.Description = model.Description;

                await _roleManager.UpdateAsync(role);

                //update role claims
                var currentClaims = await _roleManager.GetClaimsAsync(role);
                var addedClaims = model.Claims.Except(currentClaims.Select(c => c.Value));
                var removedClaims = currentClaims.Select(c => c.Value).Except(model.Claims);

                if (addedClaims.Any())
                {
                    foreach (var claim in addedClaims)
                    {
                        await _roleManager.AddClaimAsync(role, new Claim("role", claim));
                    }
                }

                if (removedClaims.Any())
                {
                    foreach (var claim in removedClaims)
                    {
                        await _roleManager.RemoveClaimAsync(role, new Claim("role", claim));
                    }
                }

                //capture claim changes
                if (addedClaims.Any() || removedClaims.Any())
                { 
                    changes.Add(new PropertyChangeModel("Claims", string.Join(",", currentClaims), string.Join(",", model.Claims)));
                }

                //TODO: Create audit event

                return true;
                
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogRoleUpdateException(model.Name, ex.Message, model);
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
