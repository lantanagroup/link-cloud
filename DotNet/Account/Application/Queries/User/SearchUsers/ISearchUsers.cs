using LantanaGroup.Link.Account.Application.Models.Responses;
using LantanaGroup.Link.Account.Application.Models.User;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public interface ISearchUsers
    {
        Task<PagedUserModel> Execute(UserSearchFilterRecord filters, CancellationToken cancellationToken = default);
    }
}
