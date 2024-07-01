using Microsoft.AspNetCore.SignalR;

namespace ChildAllowanceManager.Services;

public class NotificationHub: Hub
{
    public const string AllowanceUpdated = "AllowanceUpdated";

    public override async Task OnConnectedAsync()
    {
        // link to the correct tenant based on query param from initial connection, since cookies are not propagated
        var tenant = Context.GetHttpContext()?.Request.Query.TryGetValue("tenant", out var currentTenant) ?? false
            ? currentTenant.FirstOrDefault()
            : null;
        if (tenant is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, tenant);
        await base.OnConnectedAsync();
    }
}