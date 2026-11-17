using System.Collections.Concurrent;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace CyberManagement.API.Realtime;

public sealed class RealtimeAccountConnections(
    IHubContext<RealtimeHub> hub,
    IServiceScopeFactory scopeFactory)
{
    private readonly ConcurrentDictionary<int, EmployeeConnections> _employees = new();

    public async Task<bool> ConnectEmployeeAsync(
        int employeeId,
        string connectionId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var connections = _employees.GetOrAdd(employeeId, static _ => new EmployeeConnections());
        await connections.Gate.WaitAsync(cancellationToken);
        var addedGroups = new List<string>();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IIdentityRepository>();
            var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken);
            if (employee is null || !employee.IsActive)
                return false;

            var groups = permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.Ordinal)
                .Select(RealtimeGroups.ForPermission)
                .Prepend(RealtimeGroups.ForAccount(RealtimeAccountType.Staff, employeeId));

            foreach (var group in groups)
            {
                await hub.Groups.AddToGroupAsync(connectionId, group, cancellationToken);
                addedGroups.Add(group);
            }

            connections.GroupsByConnection[connectionId] = addedGroups;
            await hub.Clients.Client(connectionId).SendAsync("RealtimeReady", cancellationToken);
            return true;
        }
        catch
        {
            connections.GroupsByConnection.Remove(connectionId);
            foreach (var group in addedGroups)
                await hub.Groups.RemoveFromGroupAsync(connectionId, group, CancellationToken.None);
            throw;
        }
        finally
        {
            connections.Gate.Release();
        }
    }

    public async Task DisconnectEmployeeAsync(int employeeId, string connectionId)
    {
        if (!_employees.TryGetValue(employeeId, out var connections))
            return;

        await connections.Gate.WaitAsync(CancellationToken.None);
        try
        {
            connections.GroupsByConnection.Remove(connectionId);
        }
        finally
        {
            connections.Gate.Release();
        }
    }

    public async Task RevokeEmployeeAsync(int employeeId)
    {
        if (!_employees.TryGetValue(employeeId, out var connections))
            return;

        await connections.Gate.WaitAsync(CancellationToken.None);
        try
        {
            var activeConnections = connections.GroupsByConnection.ToArray();
            await Task.WhenAll(activeConnections.SelectMany(connection =>
                connection.Value.Select(group =>
                    hub.Groups.RemoveFromGroupAsync(connection.Key, group, CancellationToken.None))));

            connections.GroupsByConnection.Clear();

            await Task.WhenAll(activeConnections.Select(connection =>
                hub.Clients.Client(connection.Key).SendAsync("AccountLocked", CancellationToken.None)));
        }
        finally
        {
            connections.Gate.Release();
        }
    }

    private sealed class EmployeeConnections
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public Dictionary<string, List<string>> GroupsByConnection { get; } = new(StringComparer.Ordinal);
    }
}
