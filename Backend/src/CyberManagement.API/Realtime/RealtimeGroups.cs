using System.Globalization;
using System.Security.Claims;
using CyberManagement.Application.DTOs.Realtime;

namespace CyberManagement.API.Realtime;

public static class RealtimeGroups
{
    public static string ForAccount(RealtimeAccountType accountType, int accountId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        var type = accountType switch
        {
            RealtimeAccountType.Staff => "staff",
            RealtimeAccountType.Customer => "customer",
            _ => throw new ArgumentOutOfRangeException(nameof(accountType))
        };
        return $"account:{type}:{accountId.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string ForPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return $"permission:{permission}";
    }

    public static string? FromPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true
            || !int.TryParse(user.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || id <= 0)
            return null;

        var accountType = user.FindFirstValue("account_type");
        var role = user.FindFirstValue("role");
        if (accountType == "Customer" && role == "Customer"
            && int.TryParse(user.FindFirstValue("customer_id"), NumberStyles.None, CultureInfo.InvariantCulture, out var customerId)
            && customerId == id)
            return ForAccount(RealtimeAccountType.Customer, id);

        return accountType is null && role is ("Admin" or "Manager" or "Cashier" or "Waiter" or "Technician")
            ? ForAccount(RealtimeAccountType.Staff, id)
            : null;
    }
}
