using CyberManagement.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CyberManagement.API.Realtime;

[Authorize(Policy = ApiPolicies.RealtimeConnect)]
public sealed class RealtimeHub : Hub
{
    public const string Endpoint = "/hubs/realtime";

    public override async Task OnConnectedAsync()
    {
        var account = RealtimeGroups.FromPrincipal(Context.User)
            ?? throw new HubException("Danh tính kết nối không hợp lệ.");
        await Groups.AddToGroupAsync(Context.ConnectionId, account, Context.ConnectionAborted);

        // Customers never enter staff permission groups, even if a token contains such a claim.
        if (account.StartsWith("account:staff:", StringComparison.Ordinal))
        {
            foreach (var permission in Context.User!.FindAll("permission").Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.ForPermission(permission),
                    Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("RealtimeReady", Context.ConnectionAborted);
    }
}
