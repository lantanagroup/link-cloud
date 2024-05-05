using LantanaGroup.Link.Account.Application.Interfaces.Factories;
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
    public class GetFacilityUsers : IGetFacilityUsers
    {
        private readonly ILogger<GetFacilityUsers> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IGroupedUserModelFactory _groupedUserModelFactory;

        public GetFacilityUsers(ILogger<GetFacilityUsers> logger, IUserRepository userRepository, IGroupedUserModelFactory groupedUserModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _groupedUserModelFactory = groupedUserModelFactory ?? throw new ArgumentNullException(nameof(groupedUserModelFactory));
        }

        public async Task<IEnumerable<GroupedUserModel>> Execute(string facilityId, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId)];
            Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("GetUserById:Execute", tagList);

            try
            {
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    throw new ArgumentException("A facility id is required");
                }

                var users = await _userRepository.GetFacilityUsersAsync(facilityId, cancellationToken);

                if (users is null)
                {
                    return [];
                }
                
                List<GroupedUserModel> facilityUsers = [];
                foreach (var user in users)
                {
                    var groupedUser = _groupedUserModelFactory.Create(user);
                    facilityUsers.Add(groupedUser);
                }
                
                return facilityUsers;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogFindUsersException(ex.Message);
                throw;
            }
            finally
            {
                activity?.Stop();
            }
            
        }
    }
}
