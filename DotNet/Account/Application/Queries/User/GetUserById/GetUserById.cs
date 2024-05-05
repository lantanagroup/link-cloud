using LantanaGroup.Link.Account.Application.Interfaces.Factories;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using OpenTelemetry.Trace;
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

        public async Task<LinkUserModel> Execute(string id, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, object?>> tagList = [new KeyValuePair<string, object?>(DiagnosticNames.UserId, id)];
            Activity? activity = ServiceActivitySource.Instance.StartActivityWithTags("GetUserById:Execute", tagList);

            try
            {
                if(string.IsNullOrEmpty(id))
                {
                    throw new ArgumentException("A user id is required");
                }

                var user = await _userRepository.GetUserAsync(id, cancellationToken: cancellationToken);

                LinkUserModel userModel = user is null ? throw new ApplicationException($"User with an id of {id} was not found") 
                    : _linkUserModelFactory.Create(user);

                return userModel;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogFindUserException(id, ex.Message);
                throw;
            }
            finally
            {
                activity?.Stop();
            }
        }
    }
}
