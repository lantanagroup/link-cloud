namespace LantanaGroup.Link.Terminology.Services;

/// <summary>
/// A hosted service responsible for managing the application's startup and shutdown events.
/// Utilizes the <see cref="CodeGroupCacheService"/> to initialize necessary cache data during the startup phase.
/// Implements the <see cref="IHostedService"/> interface.
/// </summary>
public class Startup(CodeGroupCacheService codeGroupCacheService) : IHostedService
{
    /// <summary>
    /// Starts the asynchronous processing for initializing the application.
    /// Calls the necessary services to prepare required resources during application startup,
    /// such as initializing caches or other preparatory tasks.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that allows the asynchronous startup process to be canceled.</param>
    /// <return>A task that represents the asynchronous startup operation.</return>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await codeGroupCacheService.LoadCache();
    }

    /// <summary>
    /// Stops the asynchronous processing and gracefully shuts down the application.
    /// Ensures all necessary cleanup tasks are performed, such as releasing resources
    /// or saving states, before the application exits.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that allows the shutdown process to be canceled.</param>
    /// <return>A task that represents the asynchronous shutdown operation.</return>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}