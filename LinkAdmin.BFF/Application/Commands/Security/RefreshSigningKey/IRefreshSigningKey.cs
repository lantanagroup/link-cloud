namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public interface IRefreshSigningKey
    {
        Task<bool> ExecuteAsync();
    }
}
