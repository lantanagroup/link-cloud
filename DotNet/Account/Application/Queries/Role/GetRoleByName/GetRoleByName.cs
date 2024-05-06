using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.Role.GetRoleByName
{
    public class GetRoleByName : IGetRoleByName
    {
        private readonly ILogger<GetRoleByName> _logger;
        private readonly IRoleRepository _roleRepository;
        private readonly ILinkRoleModelFactory _linkRoleModelFactory;

        public GetRoleByName(ILogger<GetRoleByName> logger, IRoleRepository roleRepository, ILinkRoleModelFactory linkRoleModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _linkRoleModelFactory = linkRoleModelFactory ?? throw new ArgumentNullException(nameof(linkRoleModelFactory));
        }

        public async Task<LinkRoleModel> Execute(string name, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("GetRoleByName.Execute");

            try
            {
                var role = await _roleRepository.GetRoleByNameAsync(name, cancellationToken: cancellationToken) ?? 
                    throw new ApplicationException($"Role with name {name} not found");

                return _linkRoleModelFactory.Create(role);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogFindRoleException(name, ex.Message);
                throw;
            }
        }
    }
}
