using LantanaGroup.Link.Account.Application.Models.Responses;
using LantanaGroup.Link.Account.Application.Models.User;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public interface ISearchFacilityUsers
    {
        Task<PagedUserModel> Execute(string facilityId, UserSearchFilterRecord filters, CancellationToken cancellationToken = default);
    }
}
