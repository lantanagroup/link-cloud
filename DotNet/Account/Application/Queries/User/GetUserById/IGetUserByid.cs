using LantanaGroup.Link.Account.Application.Models;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public interface IGetUserByid
    {
        Task<LinkUserModel> Execute(string id, CancellationToken cancellationToken = default);
    }
}
