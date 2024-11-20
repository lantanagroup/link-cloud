using LantanaGroup.Link.Account.Application.Models.User;

namespace LantanaGroup.Link.Account.Application.Queries.User
{
    public interface IGetUserByid
    {
        Task<LinkUserModel> Execute(Guid id, CancellationToken cancellationToken = default);
    }
}
