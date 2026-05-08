using LantanaGroup.Link.Account.Application.Models.User;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public interface IGetRoleUsers
    {
        Task<IEnumerable<GroupedUserModel>> Execute(string role, CancellationToken cancellationToken = default);
    }
}
