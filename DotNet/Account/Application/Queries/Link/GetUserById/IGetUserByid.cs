using LantanaGroup.Link.Account.Application.Models;

namespace LantanaGroup.Link.Account.Application.Queries.Link.GetUserById
{
    public interface IGetUserByid
    {
        Task<LinkUserModel> Execute(string id, CancellationToken cancellationToken = default);
    }
}
