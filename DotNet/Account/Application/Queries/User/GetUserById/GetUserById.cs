using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public class GetUserById : IGetUserByid
    {
        private readonly ILogger<GetUserById> _logger;
        private readonly IUserRepository _userRepository;
        private readonly ILinkUserModelFactory _linkUserModelFactory;

        public GetUserById(ILogger<GetUserById> logger, IUserRepository userRepository, ILinkUserModelFactory linkUserModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _linkUserModelFactory = linkUserModelFactory ?? throw new ArgumentNullException(nameof(linkUserModelFactory));
        }

        public async Task<LinkUserModel> Execute(Guid id, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, id)];
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("GetUserById:Execute", tagList);

            try
            {
                if(id == Guid.Empty)
                {
                    throw new ArgumentException("A user id is required");
                }

                var user = await _userRepository.GetUserAsync(id, cancellationToken: cancellationToken);

                if (user is null)
                {
                    return null;
                }

                LinkUserModel userModel = _linkUserModelFactory.Create(user);

                return userModel;
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
