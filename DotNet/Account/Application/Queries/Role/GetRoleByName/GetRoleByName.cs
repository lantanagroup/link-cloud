using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.Role
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
                var role = await _roleRepository.GetRoleByNameAsync(name, cancellationToken: cancellationToken);

                if (role == null)
                {
                    return null;
                }

                return _linkRoleModelFactory.Create(role);
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
