namespace LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token
{
    internal interface ICreateSystemToken
    {
        Task<string> ExecuteAsync(string key, int timespan);
    }
}
