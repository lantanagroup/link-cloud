using Microsoft.AspNetCore.SignalR;

namespace Automation.UI.Services;

public class RunHub : Hub
{
    public Task SubscribeRun(string runId)
        => Groups.AddToGroupAsync(Context.ConnectionId, runId);
}
