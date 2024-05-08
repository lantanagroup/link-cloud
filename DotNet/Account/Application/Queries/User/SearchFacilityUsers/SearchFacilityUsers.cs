using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Responses;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public class SearchFacilityUsers : ISearchFacilityUsers
    {
        private readonly ILogger<SearchFacilityUsers> _logger;
        private readonly IUserSearchRepository _userSearchRepository;
        private readonly IGroupedUserModelFactory _groupedUserModelFactory;

        public SearchFacilityUsers(ILogger<SearchFacilityUsers> logger, IUserSearchRepository userSearchRepository, IGroupedUserModelFactory groupedUserModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userSearchRepository = userSearchRepository ?? throw new ArgumentNullException(nameof(userSearchRepository));
            _groupedUserModelFactory = groupedUserModelFactory ?? throw new ArgumentNullException(nameof(groupedUserModelFactory));
        }

        public async Task<PagedUserModel> Execute(string facilityId, UserSearchFilterRecord filters, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("SearchFacilityUsers:Execute");
            activity?.AddTag(DiagnosticNames.FacilityId, facilityId);
            activity?.EnrichWithUserSearchFilter(filters);

            try
            {
                if (string.IsNullOrWhiteSpace(facilityId))
                {
                    throw new ArgumentException("A facility id is required");
                }

                var (users, metadata) = await _userSearchRepository.FacilitySearchAsync(facilityId, filters.SearchText, filters.FilterRoleBy, filters.FilterClaimBy, filters.SortBy, filters.SortOrder, filters.PageSize, filters.PageNumber, cancellationToken);

                PagedUserModel pagedModel = new()
                {
                    Records = users.Select(x => _groupedUserModelFactory.Create(x)).ToList(),
                    Metadata = metadata
                };

                return pagedModel;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
