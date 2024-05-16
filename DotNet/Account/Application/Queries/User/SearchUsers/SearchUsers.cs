using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Responses;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public class SearchUsers : ISearchUsers
    {
        private readonly ILogger<SearchUsers> _logger;
        private readonly IUserSearchRepository _userSearchRepository;
        private readonly IGroupedUserModelFactory _groupedUserModelFactory;

        public SearchUsers(ILogger<SearchUsers> logger, IUserSearchRepository userSearchRepository, IGroupedUserModelFactory groupedUserModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userSearchRepository = userSearchRepository ?? throw new ArgumentNullException(nameof(userSearchRepository));
            _groupedUserModelFactory = groupedUserModelFactory ?? throw new ArgumentNullException(nameof(groupedUserModelFactory));
        }

        public async Task<PagedUserModel> Execute(UserSearchFilterRecord filters, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("SearchUsers:Execute");            
            activity?.EnrichWithUserSearchFilter(filters);

            try
            {
                var (users, metadata) = await _userSearchRepository.SearchAsync(filters.SearchText, filters.FilterFacilityBy, filters.FilterRoleBy, filters.FilterClaimBy, filters.IncludeDeactivatedUsers, filters.IncludeDeletedUsers, filters.SortBy, filters.SortOrder, filters.PageSize, filters.PageNumber, cancellationToken);

                PagedUserModel pagedModel = new()
                {
                    Records = users.Select(x => _groupedUserModelFactory.Create(x)).ToList(),
                    Metadata = metadata
                };

                return pagedModel;
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
