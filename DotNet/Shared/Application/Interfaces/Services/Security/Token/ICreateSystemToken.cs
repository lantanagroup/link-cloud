namespace LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token
{
    public interface ICreateSystemToken
    {
        Task<string> ExecuteAsync(string key, int timespan);
    }
}
