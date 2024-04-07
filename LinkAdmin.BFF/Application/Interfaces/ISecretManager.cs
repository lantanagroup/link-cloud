namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces
{
    public interface ISecretManager
    {
        Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken);
        Task<string> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken); 
        Task<bool> SetSecretAync(string secretName, string secretValue, CancellationToken cancellationToken);
    }
}
