using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CyberCafe.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Features;

// Phạm vi của Minh: khách hàng, nạp/thu tiền, lịch sử giao dịch và ví của khách.
// Giả định về phần của Đại (auth): principal đã đăng nhập có ClaimTypes.NameIdentifier = accountId
// và ClaimTypes.Role = "Admin" | "Staff" | "Customer".

public sealed record CustomerResponse(
    long Id, long AccountId, string DisplayName, string Phone, bool IsActive,
    bool MustChangePassword, decimal WalletBalance, DateTimeOffset CreatedAt, bool HasActiveSession);

public sealed record CreateCustomerRequest(string? DisplayName, string? Phone, string? InitialPassword);
public sealed record UpdateCustomerRequest(string? DisplayName, string? Phone);
public sealed record CustomerStatusRequest(bool? IsActive);
public sealed record CreateTopUpRequest(long CustomerId, decimal? Amount);
public sealed record TopUpActionRequest(string? Action, string? CollectionMethod);
public sealed record ClientWalletResponse(decimal WalletBalance, List<WalletTransaction> Transactions, List<TopUp> TopUps);

public static class CustomerEndpoints
{
    public const decimal MinTopUp = 10_000m;
    public const decimal MaxTopUp = 2_000_000m;
    public const decimal TopUpStep = 1_000m;
    private const string StaffRoles = "Admin,Staff";

    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(new AuthorizeAttribute { Roles = StaffRoles });

        admin.MapGet("/customers", ListCustomers);
        admin.MapGet("/customers/{id:long}", GetCustomer);
        admin.MapPost("/customers", CreateCustomer);
        admin.MapPut("/customers/{id:long}", UpdateCustomer);
        admin.MapPut("/customers/{id:long}/status", SetCustomerStatus);

        admin.MapGet("/top-ups", ListTopUps);
        admin.MapGet("/transactions", ListTransactions);
        admin.MapPost("/top-ups", CreateTopUp);
        admin.MapPost("/top-ups/{id:long}/action", ActOnTopUp);

        var client = app.MapGroup("/api/client").RequireAuthorization(new AuthorizeAttribute { Roles = "Customer" });
        client.MapGet("/wallet", GetClientWallet);
        client.MapPost("/top-ups", CreateClientTopUp);
        return app;
    }

    // ---------- Khách hàng ----------

    private static IQueryable<CustomerResponse> CustomerQuery(CyberCafeDbContext db) =>
        from c in db.Customers.AsNoTracking()
        join a in db.Accounts.AsNoTracking() on c.AccountId equals a.Id
        select new CustomerResponse(
            c.Id, a.Id, a.DisplayName, a.Phone, a.IsActive, a.MustChangePassword, c.WalletBalance, a.CreatedAt,
            db.GamingSessions.Any(s => s.CustomerId == c.Id && s.Status == GamingSessionStatus.Active));

    private static async Task<IResult> ListCustomers(CyberCafeDbContext db, CancellationToken ct) =>
        Results.Ok(await CustomerQuery(db).OrderByDescending(c => c.Id).ToListAsync(ct));

    private static async Task<IResult> GetCustomer(long id, CyberCafeDbContext db, CancellationToken ct)
    {
        var customer = await CustomerQuery(db).FirstOrDefaultAsync(c => c.Id == id, ct);
        return customer is null ? NotFound("Không tìm thấy khách hàng.") : Results.Ok(customer);
    }

    private static async Task<IResult> CreateCustomer(
        CreateCustomerRequest request, ClaimsPrincipal user, CyberCafeDbContext db, CancellationToken ct)
    {
        var actor = AccountId(user);
        if (actor is null) return Results.Unauthorized();

        var errors = new Dictionary<string, string[]>();
        var name = ValidateName(request.DisplayName, errors);
        var phone = ValidatePhone(request.Phone, errors);
        var password = request.InitialPassword;
        if (password is null || password.Length is < 8 or > 128)
            errors["initialPassword"] = ["Mật khẩu phải từ 8 đến 128 ký tự."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        if (await db.Accounts.AnyAsync(a => a.Phone == phone, ct))
            return Conflict("Số điện thoại đã tồn tại.");

        try
        {
            // Account + Customer trong MỘT transaction: không để lại tài khoản mồ côi.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var account = new Account
            {
                Phone = phone!,
                DisplayName = name!,
                PasswordHash = CustomerPasswords.Hash(password!),
                Role = AccountRole.Customer,
                MustChangePassword = true,
                IsActive = true,
                CreatedByAccountId = actor,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Accounts.Add(account);
            await db.SaveChangesAsync(ct);

            var customer = new Customer { AccountId = account.Id, WalletBalance = 0, WalletVersion = 0 };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var created = await CustomerQuery(db).FirstAsync(c => c.Id == customer.Id, ct);
            return Results.Created($"/api/admin/customers/{customer.Id}", created);
        }
        catch (Exception ex) when (WalletLedger.IsConflict(ex))
        {
            return Conflict("Số điện thoại đã tồn tại.");
        }
    }

    private static async Task<IResult> UpdateCustomer(
        long id, UpdateCustomerRequest request, CyberCafeDbContext db, CancellationToken ct)
    {
        // DTO cố ý không có walletBalance: ví chỉ đổi qua WalletLedger.
        var errors = new Dictionary<string, string[]>();
        var name = ValidateName(request.DisplayName, errors);
        var phone = ValidatePhone(request.Phone, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound("Không tìm thấy khách hàng.");
        if (await db.Accounts.AnyAsync(a => a.Phone == phone && a.Id != customer.AccountId, ct))
            return Conflict("Số điện thoại đã tồn tại.");

        try
        {
            var account = await db.Accounts.FirstAsync(a => a.Id == customer.AccountId, ct);
            account.DisplayName = name!;
            account.Phone = phone!;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (WalletLedger.IsConflict(ex))
        {
            return Conflict("Số điện thoại đã tồn tại.");
        }
        return Results.Ok(await CustomerQuery(db).FirstAsync(c => c.Id == id, ct));
    }

    private static async Task<IResult> SetCustomerStatus(
        long id, CustomerStatusRequest request, CyberCafeDbContext db, CancellationToken ct)
    {
        if (request.IsActive is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["isActive"] = ["isActive là bắt buộc (true/false)."] });

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound("Không tìm thấy khách hàng.");

        if (request.IsActive == false &&
            await db.GamingSessions.AnyAsync(s => s.CustomerId == id && s.Status == GamingSessionStatus.Active, ct))
            return Conflict("Khách đang có phiên chơi Active, hãy kết thúc phiên trước khi khóa.");

        await db.Accounts.Where(a => a.Id == customer.AccountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, request.IsActive.Value), ct);
        return Results.Ok(await CustomerQuery(db).FirstAsync(c => c.Id == id, ct));
    }

    // ---------- Nạp tiền (admin/staff) ----------

    private static async Task<IResult> ListTopUps(CyberCafeDbContext db, CancellationToken ct) =>
        Results.Ok(await db.TopUps.AsNoTracking()
            .OrderByDescending(t => t.RequestedAt).ThenByDescending(t => t.Id).ToListAsync(ct));

    private static async Task<IResult> ListTransactions(CyberCafeDbContext db, CancellationToken ct) =>
        Results.Ok(await db.WalletTransactions.AsNoTracking()
            .OrderByDescending(t => t.RecordedAt).ThenByDescending(t => t.Id).Take(500).ToListAsync(ct));

    private static async Task<IResult> CreateTopUp(CreateTopUpRequest request, CyberCafeDbContext db, CancellationToken ct) =>
        await CreateTopUpCore(db, request.CustomerId, request.Amount, requestedByCustomer: false, ct);

    private static async Task<IResult> CreateClientTopUp(
        CreateTopUpRequest request, ClaimsPrincipal user, CyberCafeDbContext db, CancellationToken ct)
    {
        // Bỏ qua request.CustomerId: luôn lấy khách từ cookie đăng nhập.
        var customerId = await CurrentCustomerId(user, db, ct);
        if (customerId is null) return NotFound("Không tìm thấy hồ sơ khách hàng.");
        return await CreateTopUpCore(db, customerId.Value, request.Amount, requestedByCustomer: true, ct);
    }

    private static async Task<IResult> CreateTopUpCore(
        CyberCafeDbContext db, long customerId, decimal? amount, bool requestedByCustomer, CancellationToken ct)
    {
        if (amount is null || amount < MinTopUp || amount > MaxTopUp || amount % TopUpStep != 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["amount"] = [$"Số tiền nạp phải từ {MinTopUp:N0} đến {MaxTopUp:N0} VND và là bội số của {TopUpStep:N0}."],
            });

        try
        {
            // Serializable để hai request đồng thời không cùng tạo được hai yêu cầu mở.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
                return NotFound("Không tìm thấy khách hàng.");
            if (await db.TopUps.AnyAsync(t => t.CustomerId == customerId &&
                    (t.Status == TopUpStatus.Pending || t.Status == TopUpStatus.CreditedUnpaid), ct))
                return Conflict("Khách còn yêu cầu nạp Pending hoặc CreditedUnpaid chưa xử lý xong.");

            var topUp = new TopUp
            {
                CustomerId = customerId,
                Amount = amount.Value,
                RequestedByCustomer = requestedByCustomer,
                Status = TopUpStatus.Pending,
                RequestedAt = DateTimeOffset.UtcNow,
            };
            db.TopUps.Add(topUp);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/top-ups/{topUp.Id}", topUp);
        }
        catch (Exception ex) when (WalletLedger.IsConflict(ex))
        {
            return Conflict("Khách còn yêu cầu nạp chưa xử lý xong, hãy thử lại.");
        }
    }

    private static async Task<IResult> ActOnTopUp(
        long id, TopUpActionRequest request, ClaimsPrincipal user, CyberCafeDbContext db, CancellationToken ct)
    {
        var actor = AccountId(user);
        if (actor is null) return Results.Unauthorized();

        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("credit" or "reject" or "collect"))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["action"] = ["action phải là credit, reject hoặc collect."] });

        CollectionMethod? method = null;
        if (action == "collect")
        {
            var raw = request.CollectionMethod?.Trim();
            if (raw is null || !Enum.GetNames<CollectionMethod>().Contains(raw, StringComparer.OrdinalIgnoreCase))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["collectionMethod"] = ["collectionMethod phải là Cash hoặc BankTransfer."] });
            method = Enum.Parse<CollectionMethod>(raw, ignoreCase: true);
        }

        var now = DateTimeOffset.UtcNow;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Chiếm quyền chuyển trạng thái bằng UPDATE có điều kiện: hai request đồng thời
            // cho cùng một top-up thì chỉ một request thắng (rows == 1), request kia nhận 409.
            int claimed;
            switch (action)
            {
                case "credit":
                    claimed = await db.TopUps.Where(t => t.Id == id && t.Status == TopUpStatus.Pending)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(t => t.Status, TopUpStatus.CreditedUnpaid)
                            .SetProperty(t => t.CreditedByAccountId, (long?)actor)
                            .SetProperty(t => t.CreditedAt, (DateTimeOffset?)now), ct);
                    break;
                case "reject":
                    claimed = await db.TopUps.Where(t => t.Id == id && t.Status == TopUpStatus.Pending)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TopUpStatus.Rejected), ct);
                    break;
                default: // collect
                    claimed = await db.TopUps.Where(t => t.Id == id && t.Status == TopUpStatus.CreditedUnpaid)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(t => t.Status, TopUpStatus.Collected)
                            .SetProperty(t => t.CollectionMethod, method)
                            .SetProperty(t => t.CollectedByAccountId, (long?)actor)
                            .SetProperty(t => t.CollectedAt, (DateTimeOffset?)now), ct);
                    break;
            }

            if (claimed == 0)
            {
                return await db.TopUps.AnyAsync(t => t.Id == id, ct)
                    ? Conflict($"Yêu cầu nạp không ở trạng thái phù hợp để {action}.")
                    : NotFound("Không tìm thấy yêu cầu nạp.");
            }

            if (action == "credit")
            {
                var topUp = await db.TopUps.AsNoTracking().FirstAsync(t => t.Id == id, ct);
                var customer = await db.Customers.FirstAsync(c => c.Id == topUp.CustomerId, ct);
                WalletLedger.Record(db, customer, WalletTransactionType.TopUpCredit, topUp.Amount, now,
                    recordedByAccountId: actor, topUpId: topUp.Id);
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (WalletLedger.IsConflict(ex))
        {
            return Conflict("Xung đột dữ liệu khi xử lý yêu cầu nạp, hãy tải lại và thử lại.");
        }

        return Results.Ok(await db.TopUps.AsNoTracking().FirstAsync(t => t.Id == id, ct));
    }

    // ---------- Ví của khách đăng nhập ----------

    private static async Task<IResult> GetClientWallet(ClaimsPrincipal user, CyberCafeDbContext db, CancellationToken ct)
    {
        var accountId = AccountId(user);
        if (accountId is null) return Results.Unauthorized();
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.AccountId == accountId, ct);
        if (customer is null) return NotFound("Không tìm thấy hồ sơ khách hàng.");

        // Luôn dùng walletBalance từ DB; danh sách bị giới hạn nên không dùng để cộng lại số dư.
        var transactions = await db.WalletTransactions.AsNoTracking().Where(t => t.CustomerId == customer.Id)
            .OrderByDescending(t => t.RecordedAt).ThenByDescending(t => t.Id).Take(200).ToListAsync(ct);
        var topUps = await db.TopUps.AsNoTracking().Where(t => t.CustomerId == customer.Id)
            .OrderByDescending(t => t.RequestedAt).ThenByDescending(t => t.Id).Take(100).ToListAsync(ct);
        return Results.Ok(new ClientWalletResponse(customer.WalletBalance, transactions, topUps));
    }

    // ---------- Tiện ích ----------

    private static long? AccountId(ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static async Task<long?> CurrentCustomerId(ClaimsPrincipal user, CyberCafeDbContext db, CancellationToken ct)
    {
        var accountId = AccountId(user);
        if (accountId is null) return null;
        return await db.Customers.AsNoTracking().Where(c => c.AccountId == accountId)
            .Select(c => (long?)c.Id).FirstOrDefaultAsync(ct);
    }

    private static IResult NotFound(string detail) => Results.Problem(detail: detail, title: "Không tìm thấy", statusCode: StatusCodes.Status404NotFound);
    private static IResult Conflict(string detail) => Results.Problem(detail: detail, title: "Xung đột dữ liệu", statusCode: StatusCodes.Status409Conflict);

    // Quy tắc của Đại: tên 1-100 ký tự sau trim, không ký tự điều khiển; phone bắt đầu 0, 10-11 chữ số.
    // TODO: thay bằng ManagementValidation.cs khi Đại đưa file dùng chung lên.
    private static string? ValidateName(string? value, Dictionary<string, string[]> errors)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100 || name.Any(char.IsControl))
        {
            errors["displayName"] = ["Tên phải từ 1 đến 100 ký tự và không chứa ký tự điều khiển."];
            return null;
        }
        return name;
    }

    private static string? ValidatePhone(string? value, Dictionary<string, string[]> errors)
    {
        var phone = value?.Trim();
        if (phone is null || !PhonePattern.IsMatch(phone))
        {
            errors["phone"] = ["Số điện thoại phải bắt đầu bằng 0 và gồm 10-11 chữ số."];
            return null;
        }
        return phone;
    }

    private static readonly Regex PhonePattern = new("^0[0-9]{9,10}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
}

/// <summary>
/// TẠM THỜI: băm mật khẩu cho khách mới. Định dạng phải khớp bộ xác thực đăng nhập của Đại;
/// thay bằng service băm chung của Đại ngay khi có để login đọc được hash này.
/// </summary>
internal static class CustomerPasswords
{
    private const int Iterations = 210_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}