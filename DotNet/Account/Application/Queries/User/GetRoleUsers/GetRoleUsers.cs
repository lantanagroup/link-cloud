using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public class GetRoleUsers : IGetRoleUsers
    {
        private readonly ILogger<GetRoleUsers> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IGroupedUserModelFactory _groupedUserModelFactory;

        public GetRoleUsers(ILogger<GetRoleUsers> logger, IUserRepository userRepository, IGroupedUserModelFactory groupedUserModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _groupedUserModelFactory = groupedUserModelFactory ?? throw new ArgumentNullException(nameof(groupedUserModelFactory));
        }

        public async Task<IEnumerable<GroupedUserModel>> Execute(string role, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.Role, role)];
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("GetUserById:Execute", tagList);                   

            try
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    throw new ArgumentException("A role is required");
                }

                var users = await _userRepository.GetRoleUsersAsync(role, cancellationToken);
                
                if (users is null)
                {
                    return [];
                }

                List<GroupedUserModel> roleUsers = [];
                foreach (var user in users)
                {
                    roleUsers.Add(_groupedUserModelFactory.Create(user));
                }

                return roleUsers;                
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
