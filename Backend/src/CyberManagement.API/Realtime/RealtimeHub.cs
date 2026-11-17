using CyberManagement.API.Security;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CyberManagement.API.Realtime;

[Authorize(Policy = ApiPolicies.RealtimeConnect)]
public sealed class RealtimeHub : Hub
{
    private readonly RealtimeAccountConnections _accountConnections;

    public RealtimeHub(RealtimeAccountConnections accountConnections) =>
        _accountConnections = accountConnections;

    public const string Endpoint = "/hubs/realtime";

    public override async Task OnConnectedAsync()
    {
        var account = RealtimeGroups.FromPrincipal(Context.User)
            ?? throw new HubException("Danh tính kết nối không hợp lệ.");

        await base.OnConnectedAsync();

        if (account.StartsWith("account:staff:", StringComparison.Ordinal))
        {
            var employeeIdClaim = Context.User!.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.IsNullOrEmpty(employeeIdClaim) && int.TryParse(employeeIdClaim, out var employeeId))
            {
                var permissions = Context.User!.FindAll("permission").Select(claim => claim.Value);
                if (!await _accountConnections.ConnectEmployeeAsync(
                        employeeId, Context.ConnectionId, permissions, Context.ConnectionAborted))
                    throw new HubException("Tài khoản đã bị khóa. Vui lòng đăng nhập lại.");
            }
            else
            {
                throw new HubException("Danh tính nhân viên không hợp lệ.");
            }
        }
        else if (account.StartsWith("account:customer:", StringComparison.Ordinal))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, account, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.TournamentPublic,
                Context.ConnectionAborted);
            await Clients.Caller.SendAsync("RealtimeReady", Context.ConnectionAborted);
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, account, Context.ConnectionAborted);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var account = RealtimeGroups.FromPrincipal(Context.User);
        if (account?.StartsWith("account:staff:", StringComparison.Ordinal) == true
            && int.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var employeeId))
            await _accountConnections.DisconnectEmployeeAsync(employeeId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
