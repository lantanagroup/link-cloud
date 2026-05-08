using LantanaGroup.Link.Account.Application.Models.User;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public interface IGetUserByEmail
    {
        Task<LinkUserModel> Execute(string email, CancellationToken cancellationToken = default);
    }
}
