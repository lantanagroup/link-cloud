using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Automation.UI.Services;

[Authorize]
public class RunHub : Hub
{
    public Task SubscribeRun(string runId)
        => Groups.AddToGroupAsync(Context.ConnectionId, runId);
}
