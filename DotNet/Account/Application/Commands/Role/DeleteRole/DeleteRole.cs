using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.AspNetCore.Identity;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role.DeleteRole
{
    public class DeleteRole : IDeleteRole
    {
        private readonly ILogger<DeleteRole> _logger;
        private readonly RoleManager<LinkRole> _roleManager;

        public DeleteRole(ILogger<DeleteRole> logger, RoleManager<LinkRole> roleManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        }

        public async Task<bool> Execute(ClaimsPrincipal? requestor, string roleId, CancellationToken cancellationToken = default)
        {
            Activity? activity = ServiceActivitySource.Instance.StartActivity("DeleteRole:Execute");

            try
            { 
                var role = await _roleManager.FindByIdAsync(roleId) ?? throw new ApplicationException($"Role with id {roleId} not found");
                activity?.AddTag(DiagnosticNames.Role, role.Name);

                var result = await _roleManager.DeleteAsync(role);

                if (!result.Succeeded)
                {
                    throw new ApplicationException($"Failed to delete role {role.Name}");
                }

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogRoleDeletionException(roleId, ex.Message);
                throw;
            }   
        }
    }
}
