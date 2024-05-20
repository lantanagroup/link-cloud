using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.Role
{
    public class GetRoles : IGetRoles
    {
        private readonly ILogger<GetRoles> _logger;
        private readonly IRoleRepository _roleRepository;
        private readonly IListRoleModelFactory _listRoleModelFactory;

        public GetRoles(ILogger<GetRoles> logger, IRoleRepository roleRepository, IListRoleModelFactory listRoleModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _listRoleModelFactory = listRoleModelFactory ?? throw new ArgumentNullException(nameof(listRoleModelFactory));
        }

        public async Task<IEnumerable<ListRoleModel>> Execute(CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("GetRole:Execute");

            try
            {
                var roles = await _roleRepository.GetRolesAsync(cancellationToken);
                
                var roleList = roles.Select(x => _listRoleModelFactory.Create(x)).ToList();

                return roleList;
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);             
                throw;
            }
        }
    }
}
