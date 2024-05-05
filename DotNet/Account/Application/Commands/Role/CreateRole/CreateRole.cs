using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
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

        public CreateRole(ILogger<CreateRole> logger, RoleManager<LinkRole> roleManager, ILinkRoleModelFactory roleModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _roleModelFactory = roleModelFactory ?? throw new ArgumentNullException(nameof(roleModelFactory));
        }

        public async Task<LinkRoleModel> Execute(string name, string? description, List<string>? claims, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("CreateRole:Execute");

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("A role name is required");
                }

                var role = new LinkRole
                {
                    Name = name,
                    Description = description                    
                };

                var result = await _roleManager.CreateAsync(role);

                if (!result.Succeeded)
                {
                    throw new ApplicationException($"Failed to create role {name}");
                }

                //if claims were provided, add them
                if (claims is not null)
                {
                    foreach (var claim in claims)
                    {
                        result = await _roleManager.AddClaimAsync(role, 
                            new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, claim));

                        if (!result.Succeeded)
                        {
                            throw new ApplicationException($"Failed to add claim {claim} to role {name}");
                        }
                    }
                }

                var roleModel = _roleModelFactory.Create(role);

                return roleModel;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogRoleCreated(name, ex.Message, 
                    new LinkRoleModel(string.Empty, name, description ?? string.Empty, claims ?? []));
                throw;
            }
        }
    }
}
