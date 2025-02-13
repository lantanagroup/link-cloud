namespace Terminology.Services;

public class Startup(CodeGroupCacheService codeGroupCacheService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        codeGroupCacheService.LoadCache();
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}