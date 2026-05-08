namespace LantanaGroup.Link.Shared.Application.Interfaces.Services
{
    public interface ISecretManager
    {
        Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken);
        Task<string> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken);
        Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken);
    }
}
