using Microsoft.Extensions.DependencyInjection;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// Tells the person on the dashboard how a long-running command is going, as link-lbw's AppHost does: a notice
/// at the top while it runs, then one that stays until dismissed, green on success and red on failure.
/// </summary>
internal static class CommandNotifications
{
    /// <summary>
    /// Runs <paramref name="work"/> with a "running" notice, then shows its outcome. The outcome uses the result's
    /// own message, so what the dashboard shows matches what the command returns. A cancelled result shows nothing.
    /// </summary>
    /// <remarks>
    /// A notification's task only finishes when the person dismisses it, so none is awaited. Cancelling the
    /// running notice's token takes it off the dashboard.
    /// </remarks>
    public static async Task<ExecuteCommandResult> RunWithNotificationsAsync(
        ExecuteCommandContext context, string title, string runningMessage, Func<Task<ExecuteCommandResult>> work)
    {
        var interaction = context.Services.GetRequiredService<IInteractionService>();
        using var running = new CancellationTokenSource();

        if (interaction.IsAvailable)
            _ = interaction.PromptNotificationAsync(title, runningMessage,
                new NotificationInteractionOptions { Intent = MessageIntent.Information }, running.Token);

        ExecuteCommandResult result;
        try
        {
            result = await work();
        }
        finally
        {
            await running.CancelAsync();
        }

        if (interaction.IsAvailable && !result.Canceled)
            _ = interaction.PromptNotificationAsync(
                title,
                result.Message ?? (result.Success ? "Done." : "It failed. The AppHost console has the details."),
                new NotificationInteractionOptions { Intent = result.Success ? MessageIntent.Success : MessageIntent.Error },
                CancellationToken.None);

        return result;
    }
}
