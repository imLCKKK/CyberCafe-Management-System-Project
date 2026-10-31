using System.Globalization;
using System.Security.Claims;
using CyberManagement.Application.Interfaces;

namespace CyberManagement.API.Security;

// Chỉ đọc claims do cơ chế đăng nhập xác thực cung cấp; không lấy danh tính từ body hoặc header tự khai.
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public long AccountId => ReadAccountId(Principal);
    public long? CustomerId => ReadEntityId(Principal, "customer_id");
    public long? EmployeeId => ReadEntityId(Principal, "employee_id");
    public string Role => Principal.Identity is ClaimsIdentity identity && identity.IsAuthenticated
        ? Principal.FindFirst(identity.RoleClaimType)?.Value ?? string.Empty
        : string.Empty;
    public bool IsActive => HasActiveAccount(Principal);
    public bool MustChangePassword => ReadFlag(Principal, "must_change_password");

    // Thiếu account_id hoặc is_active thì không cấp quyền, kể cả khi có claim Role.
    public static bool HasActiveAccount(ClaimsPrincipal principal) =>
        ReadAccountId(principal) > 0 && ReadFlag(principal, "is_active");

    private static long ReadAccountId(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return 0;
        var value = principal.FindFirst("account_id")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : 0;
    }

    private static long? ReadEntityId(ClaimsPrincipal principal, string claim)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        return long.TryParse(principal.FindFirst(claim)?.Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    private static bool ReadFlag(ClaimsPrincipal principal, string claim) =>
        principal.Identity?.IsAuthenticated == true &&
        bool.TryParse(principal.FindFirst(claim)?.Value, out var value) && value;
}
