using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public class GetLinkUserEntity : IGetLinkUserEntity
    {
        private readonly ILogger<GetLinkUserEntity> _logger;
        private readonly IUserRepository _userRepository;

        public GetLinkUserEntity(ILogger<GetLinkUserEntity> logger, IUserRepository userRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        public async Task<LinkUser> Execute(Guid id, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, id)];
            using Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("GetLinkUserEntity:Execute", tagList);

            try
            {

                if (id == Guid.Empty)
                {
                    throw new ArgumentException("A user id is required");
                }

                var user = await _userRepository.GetUserAsync(id, cancellationToken: cancellationToken);

                return user;
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }


        }
    }
}
