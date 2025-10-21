using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Automation.UI.Services;

[Authorize]
public class RunHub : Hub
{
    public const string DashboardGroup = "dashboard";

    public Task SubscribeRun(string runId)
        => Groups.AddToGroupAsync(Context.ConnectionId, runId);

    public Task SubscribeDashboard()
        => Groups.AddToGroupAsync(Context.ConnectionId, DashboardGroup);
}
