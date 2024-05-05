using LantanaGroup.Link.Account.Application.Models;

namespace LantanaGroup.Link.Account.Application.Queries.Link.GetUserByEmail
{
    public interface IGetUserByEmail
    {
        Task<LinkUserModel> Execute(string email, CancellationToken cancellationToken = default);
    }
}
