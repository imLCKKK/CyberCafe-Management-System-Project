using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class SchemaContractChecks
{
    // Chỉ chạy trong database GUID do bộ kiểm tra tạo; không phải service thu/hoàn thật.
    public static async Task RunAsync(ApplicationDbContext db, Action<string, bool> check)
    {
        var customerId = await db.Customers.Select(x => x.CustomerId).FirstAsync();
        var employeeId = await db.Employees.Where(x => x.Username == "admin").Select(x => x.EmployeeId).SingleAsync();
        var productId = await db.Products.Select(x => x.ProductId).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var order = new Order { CustomerId = customerId, EmployeeId = employeeId, TotalAmount = 10000 };
        order.Details.Add(new OrderDetail { ProductId = productId, ProductNameSnapshot = "Tên món lúc đặt", Quantity = 1, UnitPrice = 10000 });
        var invoice = new Invoice { CustomerId = customerId, EmployeeId = employeeId, Subtotal = 15000, TotalAmount = 15000 };
        var secondOrder = new Order { CustomerId = customerId, EmployeeId = employeeId, TotalAmount = 10000 };
        var secondInvoice = new Invoice { CustomerId = customerId, EmployeeId = employeeId, Subtotal = 10000, TotalAmount = 10000 };
        var thirdInvoice = new Invoice { CustomerId = customerId, EmployeeId = employeeId, Subtotal = 10000, TotalAmount = 10000 };
        var session = new GamingSession
        {
            CustomerId = customerId, StartTime = now.UtcDateTime,
            Computer = new Computer { ComputerName = "SCHEMA_CHECK", Room = new ComputerRoom { RoomName = "SCHEMA_CHECK" }, ComputerType = new ComputerType { TypeName = "SCHEMA_CHECK", PricePerHour = 10000 } }
        };
        var charge = new SessionCharge { GamingSession = session, StartTime = now.UtcDateTime, EndTime = now.AddMinutes(30).UtcDateTime, Amount = 5000 };
        db.AddRange(order, secondOrder, invoice, secondInvoice, thirdInvoice, charge);
        await db.SaveChangesAsync();
        var orderId = order.OrderId;
        var detailId = order.Details.Single().OrderDetailId;
        var invoiceId = invoice.InvoiceId;
        var secondInvoiceId = secondInvoice.InvoiceId;
        var thirdInvoiceId = thirdInvoice.InvoiceId;
        var chargeId = charge.ChargeId;
        db.ChangeTracker.Clear();

        Payment Cash(string key, bool success = false) => new()
        {
            OrderId = orderId, OperationKey = key, Method = "Cash", Amount = 10000,
            Status = success ? "Succeeded" : "Pending",
            PaymentTime = success ? now : null, ConfirmedAt = success ? now : null,
            PaymentTimeBasis = success ? "Source" : null
        };
        var payment = Cash("cash:before-invoice", true);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        check("SQL payment persists before invoice with a concrete order source", payment.InvoiceId is null && payment.PaymentId > 0);
        var paymentId = payment.PaymentId;
        db.ChangeTracker.Clear();
        await Reject("Payment operation key is unique", Cash("cash:before-invoice"), "IX_Payments_OperationKey");
        var missingSource = Cash("cash:missing-source"); missingSource.OrderId = null;
        await Reject("Payment cannot be orphaned", missingSource, "CK_Payments_Source");
        var twoSources = Cash("cash:two-sources"); twoSources.SessionChargeId = chargeId;
        await Reject("Payment source is XOR", twoSources, "CK_Payments_Source");
        var missingConfirmation = Cash("cash:missing-confirmation", true); missingConfirmation.ConfirmedAt = null;
        await Reject("Succeeded payment requires confirmation time", missingConfirmation, "CK_Payments_Time");
        var wrongBasis = Cash("cash:wrong-basis", true); wrongBasis.PaymentTimeBasis = "Confirmation"; wrongBasis.PaymentTime = now.AddMinutes(-1);
        await Reject("Confirmation basis uses the confirmation timestamp", wrongBasis, "CK_Payments_Time");
        var walletWithoutLedger = Cash("wallet:no-ledger", true); walletWithoutLedger.Method = "Wallet";
        await Reject("Succeeded wallet mirror requires a ledger link", walletWithoutLedger, "CK_Payments_Method");
        var refund = Cash("cash:refund", true);
        refund.Type = "Refund"; refund.RefundOfPaymentId = paymentId; refund.Reason = "Hủy dòng món"; refund.Amount = 2000;
        db.Payments.Add(refund); await db.SaveChangesAsync();
        check("SQL refund is a separate positive row linked to the original payment", refund.PaymentId != paymentId && refund.Amount == 2000);
        db.ChangeTracker.Clear();
        var expiredRefund = Cash("cash:expired-refund"); expiredRefund.Type = "Refund"; expiredRefund.RefundOfPaymentId = paymentId; expiredRefund.Reason = "Hủy món"; expiredRefund.Status = "Expired";
        await Reject("Refund has no Expired state", expiredRefund, "CK_Payments_Type");

        var gatewayPayment = Cash("gateway:charge", true);
        gatewayPayment.Method = "ZaloPay"; gatewayPayment.MerchantTransactionId = "261107_scrum13_charge"; gatewayPayment.GatewayTransactionId = "261107000000001";
        db.Payments.Add(gatewayPayment); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var duplicateGateway = Cash("gateway:duplicate", true);
        duplicateGateway.Method = "ZaloPay"; duplicateGateway.MerchantTransactionId = "261107_scrum13_other"; duplicateGateway.GatewayTransactionId = gatewayPayment.GatewayTransactionId;
        await Reject("Provider charge cannot be recorded twice", duplicateGateway, "IX_Payments_Method_GatewayTransactionId");
        var gatewayRefund = Cash("gateway:refund");
        gatewayRefund.Method = "ZaloPay"; gatewayRefund.Type = "Refund"; gatewayRefund.RefundOfPaymentId = gatewayPayment.PaymentId; gatewayRefund.Reason = "Đối soát hoàn";
        db.Payments.Add(gatewayRefund); await db.SaveChangesAsync();
        var attempt = new RefundAttempt { PaymentId = gatewayRefund.PaymentId, ProviderAppId = 2553, MerchantRefundId = "261107_2553_refund1" };
        db.RefundAttempts.Add(attempt); await db.SaveChangesAsync();
        check("Refund attempt metadata is stored without an extra money row", attempt.RefundAttemptId > 0);
        db.ChangeTracker.Clear();
        await Reject("Merchant refund ID is unique per app", new RefundAttempt { PaymentId = gatewayRefund.PaymentId, ProviderAppId = 2553, MerchantRefundId = attempt.MerchantRefundId }, "IX_RefundAttempts_ProviderAppId_MerchantRefundId");
        await Reject("Attempt states are constrained", new RefundAttempt { PaymentId = gatewayRefund.PaymentId, ProviderAppId = 2553, MerchantRefundId = "261107_2553_badstate", Status = "Unknown" }, "CK_RefundAttempts_Values");

        var promotion = new Promotion { Code = "SCRUM13_PERCENT-1", PromoName = "Test phần trăm", DiscountType = "Percent", DiscountValue = 12.5m, StartDate = now, EndDate = now.AddDays(1) };
        db.Promotions.Add(promotion); await db.SaveChangesAsync();
        check("Percent precision and unlimited voucher limits persist", promotion.DiscountValue == 12.5m && promotion.MaxDiscountAmount is null && promotion.TotalUsageLimit is null);
        db.ChangeTracker.Clear();
        await Reject("Fixed voucher rejects fractional VND", new Promotion { Code = "SCRUM13_BAD_FIXED", PromoName = "Bad", DiscountType = "Fixed", DiscountValue = 1.5m, StartDate = now, EndDate = now.AddDays(1) }, "CK_Promotions_Discount");
        var usage = new PromotionUsage { PromotionId = promotion.PromotionId, OrderId = orderId, CustomerId = customerId, UsedAt = now };
        db.PromotionUsages.Add(usage); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject("An order has at most one voucher usage", new PromotionUsage { PromotionId = promotion.PromotionId, OrderId = orderId, CustomerId = customerId, UsedAt = now }, "IX_PromotionUsages_OrderId");
        await Reject("Voucher release cannot precede usage", new PromotionUsage { PromotionId = promotion.PromotionId, OrderId = secondOrder.OrderId, CustomerId = customerId, UsedAt = now, ReleasedAt = now.AddSeconds(-1) }, "CK_PromotionUsages_Release");

        db.InvoiceIssueRequests.AddRange(
            new InvoiceIssueRequest { OperationKey = "issue:Case_A-1", RequestHash = new string('a', 64), InvoiceId = invoiceId },
            new InvoiceIssueRequest { OperationKey = "issue:case_a-1", RequestHash = new string('b', 64), InvoiceId = secondInvoiceId });
        await db.SaveChangesAsync();
        check("Invoice operation keys use case-sensitive storage", await db.InvoiceIssueRequests.CountAsync() == 2);
        db.ChangeTracker.Clear();
        await Reject("Invoice issue key cannot be reused", new InvoiceIssueRequest { OperationKey = "issue:Case_A-1", RequestHash = new string('a', 64), InvoiceId = thirdInvoiceId }, "PK_InvoiceIssueRequests");
        await Reject("Invoice has one durable issue request", new InvoiceIssueRequest { OperationKey = "issue:another", RequestHash = new string('a', 64), InvoiceId = invoiceId }, "IX_InvoiceIssueRequests_InvoiceId");
        await Reject("Invoice request hash must be lowercase hex", new InvoiceIssueRequest { OperationKey = "issue:badhash", RequestHash = new string('A', 64), InvoiceId = thirdInvoiceId }, "CK_InvoiceIssueRequests_Hash");
        await Reject("Invoice operation key has the contracted ASCII format", new InvoiceIssueRequest { OperationKey = "issue:bad key", RequestHash = new string('a', 64), InvoiceId = thirdInvoiceId }, "CK_InvoiceIssueRequests_Key");
        db.InvoiceOrders.Add(new InvoiceOrder { OrderId = orderId, InvoiceId = invoiceId });
        db.InvoiceDetails.AddRange(
            new InvoiceDetail { InvoiceId = invoiceId, OrderDetailId = detailId, ItemType = "Food", Description = "Tên món lúc đặt", Quantity = 1, UnitPrice = 10000, Amount = 10000 },
            new InvoiceDetail { InvoiceId = invoiceId, SessionChargeId = chargeId, ItemType = "Session", Description = "Phí phiên", Quantity = 1, UnitPrice = 5000, Amount = 5000 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check("Callback source resolves the invoice by order and session charge", await db.InvoiceOrders.Where(x => x.OrderId == orderId).Select(x => x.InvoiceId).SingleAsync() == invoiceId && await db.InvoiceDetails.Where(x => x.SessionChargeId == chargeId).Select(x => x.InvoiceId).SingleAsync() == invoiceId);
        await Reject("An order cannot belong to two invoices", new InvoiceOrder { OrderId = orderId, InvoiceId = secondInvoiceId }, "IX_InvoiceOrders_OrderId");
        await Reject("A session charge cannot be invoiced twice", new InvoiceDetail { InvoiceId = secondInvoiceId, SessionChargeId = chargeId, ItemType = "Session", Description = "Duplicate", Quantity = 1, UnitPrice = 5000, Amount = 5000 }, "IX_InvoiceDetails_SessionChargeId");
        await Reject("An order detail cannot be invoiced twice", new InvoiceDetail { InvoiceId = secondInvoiceId, OrderDetailId = detailId, ItemType = "Food", Description = "Duplicate", Quantity = 1, UnitPrice = 10000, Amount = 10000 }, "IX_InvoiceDetails_OrderDetailId");
        await Reject("Invoice detail requires a source", new InvoiceDetail { InvoiceId = secondInvoiceId, ItemType = "Food", Description = "Missing", Quantity = 1, UnitPrice = 10000, Amount = 10000 }, "CK_InvoiceDetails_Source");

        var movement = new InventoryTransaction { ProductId = productId, Type = "Adjust", Quantity = 1, Before = 20, After = 21, Reason = "Kiểm tra schema", ReferenceType = "Manual" };
        db.InventoryTransactions.Add(movement); await db.SaveChangesAsync();
        check("System inventory movement supports a null employee without a fake identity", movement.EmployeeId is null && movement.InvTransId > 0);
        db.ChangeTracker.Clear();
        await Reject("Stock history enforces before plus delta equals after", new InventoryTransaction { ProductId = productId, Type = "Adjust", Quantity = 1, Before = 20, After = 99, Reason = "Bad", ReferenceType = "Manual" }, "CK_InventoryTransactions_Values");
        var sale = new InventoryTransaction { ProductId = productId, Type = "Sale", Quantity = -1, Before = 20, After = 19, Reason = "Xuất món", ReferenceType = "Order", OrderId = orderId, OrderDetailId = detailId };
        db.InventoryTransactions.Add(sale); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject("Sale movement cannot duplicate its order detail", new InventoryTransaction { ProductId = productId, Type = "Sale", Quantity = -1, Before = 20, After = 19, Reason = "Duplicate", ReferenceType = "Order", OrderId = orderId, OrderDetailId = detailId }, "IX_InventoryTransactions_OrderDetailId_Type");
        check("No model changes are missing a migration", !db.Database.HasPendingModelChanges());
        try { await db.GetService<IMigrator>().MigrateAsync(InventoryCheckDatabase.BaselineMigration); throw new InvalidOperationException("Downgrade discarded contract data."); }
        catch (SqlException ex) when (ex.Number == 51003) { check("Downgrade refuses to discard contract data", true); }
        check("Refused downgrade preserves issue keys and current migration", await db.InvoiceIssueRequests.CountAsync() == 2 && (await db.Database.GetAppliedMigrationsAsync()).Count() == 5);

        async Task Reject<T>(string name, T entity, string constraint) where T : class
        {
            db.Set<T>().Add(entity);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Message.Contains(constraint, StringComparison.Ordinal))
            { check(name, true); return; }
            finally { db.ChangeTracker.Clear(); }
            throw new InvalidOperationException($"{name}: expected SQL constraint {constraint} to reject the row.");
        }
    }

    public static async Task RunUpgradeGuardsAsync(string connection, Action<string, bool> check)
    {
        var fixture = new InventoryCheckDatabase(connection);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(); fixture.Configure(options);
        try
        {
            await using var db = new ApplicationDbContext(options.Options);
            await fixture.InitializeAsync(db, baselineOnly: true);
            await db.Database.ExecuteSqlRawAsync("INSERT dbo.Categories (CategoryName,IsActive) VALUES (N'UPGRADE_CHECK',1); INSERT dbo.Products (CategoryId,ProductName,Stock,IsActive,Price) SELECT CategoryId,N'UPGRADE_CHECK',1,1,10000.25 FROM dbo.Categories WHERE CategoryName=N'UPGRADE_CHECK';");
            try { await db.Database.MigrateAsync(); throw new InvalidOperationException("Fractional VND was silently migrated."); }
            catch (SqlException ex) when (ex.Number == 51001) { check("Upgrade stops instead of rounding historical VND", true); }
            check("Rejected migration leaves historical data and migration history intact", await db.Database.SqlQueryRaw<decimal>("SELECT Price AS Value FROM dbo.Products WHERE ProductName=N'UPGRADE_CHECK'").SingleAsync() == 10000.25m && (await db.Database.GetAppliedMigrationsAsync()).Count() == 4);
            await db.Database.ExecuteSqlRawAsync("UPDATE dbo.Products SET Price=10000 WHERE ProductName=N'UPGRADE_CHECK'; INSERT dbo.Promotions (PromoName,DiscountType,DiscountValue,StartDate,EndDate,IsActive) VALUES (N'LEGACY_CHECK','Percent',10,'2026-01-01','2027-01-01',1);");
            try { await db.Database.MigrateAsync(); throw new InvalidOperationException("Legacy source rows were guessed."); }
            catch (SqlException ex) when (ex.Number == 51002) { check("Upgrade refuses to invent legacy snapshots and payment sources", true); }
            check("Refused upgrade keeps the legacy voucher", await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Promotions WHERE PromoName=N'LEGACY_CHECK'").SingleAsync() == 1 && (await db.Database.GetAppliedMigrationsAsync()).Count() == 4);
            // Chỉ fixture của database GUID này được thay đổi để thử tiếp đường nâng hợp lệ.
            await db.Database.ExecuteSqlRawAsync("DELETE dbo.Promotions WHERE PromoName=N'LEGACY_CHECK';");
            await db.Database.MigrateAsync();
            check("Upgrade preserves safe existing products and appends the fifth migration", await db.Products.SingleAsync(x => x.ProductName == "UPGRADE_CHECK") is { Stock: 1, Price: 10000m } && (await db.Database.GetAppliedMigrationsAsync()).Count() == 5);
        }
        finally { await fixture.CleanupAsync(); }
    }
}
