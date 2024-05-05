using LantanaGroup.Link.Account.Application.Interfaces.Factories;
using LantanaGroup.Link.Account.Application.Interfaces.Persistence;
using LantanaGroup.Link.Account.Application.Models.Responses;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Infrastructure.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public class SearchFacilityUsers : ISearchFacilityUsers
    {
        private readonly ILogger<SearchFacilityUsers> _logger;
        private readonly IUserSearchRepository _userSearchRepository;
        private readonly ILinkUserModelFactory _linkUserModelFactory;

        public SearchFacilityUsers(ILogger<SearchFacilityUsers> logger, IUserSearchRepository userSearchRepository, ILinkUserModelFactory linkUserModelFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userSearchRepository = userSearchRepository ?? throw new ArgumentNullException(nameof(userSearchRepository));
            _linkUserModelFactory = linkUserModelFactory ?? throw new ArgumentNullException(nameof(linkUserModelFactory)); 
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
                    Records = users.Select(x => _linkUserModelFactory.Create(x)).ToList(),
                    Metadata = metadata
                };

                return pagedModel;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.RecordException(ex);
                _logger.LogSearchUsersException(ex.Message);
                throw;
            }

        }
    }
}
