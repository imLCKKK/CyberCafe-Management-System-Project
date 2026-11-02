using System.Globalization;
using System.Security.Claims;
using CyberCafe.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Features;

// Phạm vi của Minh (S1-M-1): khách đăng nhập đọc hồ sơ CỦA CHÍNH MÌNH.
// Không nhận id từ URL/body: hồ sơ luôn lấy theo accountId trong cookie đăng nhập,
// nên không có đường nào để xem hồ sơ của người khác.

public sealed record ClientProfileResponse(
    long CustomerId, long AccountId, string DisplayName, string Phone,
    bool IsActive, bool MustChangePassword, decimal WalletBalance,
    bool HasActiveSession, decimal? ActiveHourlyRate);

public static class ClientProfileEndpoint
{
    public static IEndpointRouteBuilder MapClientProfileEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/client")
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Customer" })
            .MapGet("/profile", GetProfile);
        return app;
    }

    private static async Task<IResult> GetProfile(ClaimsPrincipal user, CyberCafeDbContext db, CancellationToken ct)
    {
        if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier),
                NumberStyles.None, CultureInfo.InvariantCulture, out var accountId))
            return Results.Unauthorized();

        var profile = await (
            from c in db.Customers.AsNoTracking()
            join a in db.Accounts.AsNoTracking() on c.AccountId equals a.Id
            where a.Id == accountId
            select new ClientProfileResponse(
                c.Id, a.Id, a.DisplayName, a.Phone, a.IsActive, a.MustChangePassword, c.WalletBalance,
                db.GamingSessions.Any(s => s.CustomerId == c.Id && s.Status == GamingSessionStatus.Active),
                db.GamingSessions.Where(s => s.CustomerId == c.Id && s.Status == GamingSessionStatus.Active)
                    .Select(s => (decimal?)s.HourlyRateSnapshot).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);

        return profile is null
            ? Results.Problem(detail: "Không tìm thấy hồ sơ khách hàng.", title: "Không tìm thấy", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(profile);
    }
}