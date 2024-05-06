using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Retry.Commands
{
    public interface ICreateRetryEntity
    {
        Task<bool> Execute(RetryEntity? retry, CancellationToken cancellationToken = default);
    }
}
