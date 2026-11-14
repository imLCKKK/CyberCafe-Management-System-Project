using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class WalletLedgerChecks
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var checks = 0;
        await using (var scope = services.CreateAsyncScope())
        {
            if (!scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                Console.WriteLine("SKIP shared credit/debit/refund checks require a real SQL Server transaction.");
                return 0;
            }
        }
        var employeeId = await StaffId();
        var actor = new WalletActor("Staff", employeeId);
        var customerId = await NewCustomer("0780000101");
        var otherId = await NewCustomer("0780000102");
        var now = DateTimeOffset.UtcNow;
        var credit = Mutation(customerId, WalletMutationKind.Credit, 100000, "credit-1");
        await using (var scope = services.CreateAsyncScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
            await Throws<InvalidOperationException>("Wallet writer refuses to own an implicit transaction",
                () => writer.RecordAsync(credit, CancellationToken.None));
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await writer.RecordAsync(credit, CancellationToken.None);
            Check("Credit remains an unflushed entry owned by the caller", result.Entry.TransactionId == 0
                && db.Entry(result.Entry).State == EntityState.Added && !result.Replayed
                && !await db.WalletTransactions.AnyAsync(row => row.OperationKey == credit.OperationKey));
            var sameScope = await writer.RecordAsync(credit, CancellationToken.None);
            Check("Repeated Record before flush reuses its tracked entry", sameScope.Replayed
                && ReferenceEquals(sameScope.Entry, result.Entry)
                && db.ChangeTracker.Entries<WalletTransaction>().Count(row => row.State == EntityState.Added) == 1);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await Snapshot("Committed credit atomically changes balance and one ledger", customerId, 100000, 1);
        var replay = await Commit(credit with { OccurredAtUtc = now.AddMinutes(1) });
        Check("Committed exact replay returns the original entry", replay.Replayed && replay.Entry.Amount == 100000);
        await Snapshot("Credit replay never changes money", customerId, 100000, 1);
        foreach (var changed in new[]
        {
            credit with { Amount = 100001 }, credit with { CustomerId = otherId },
            credit with { SourceId = "different-source" }, credit with { Description = "different audit payload" },
            credit with { Actor = new WalletActor("System") }
        })
            await Refused<BusinessRuleException>("Same key with changed wallet payload conflicts", changed);
        await Refused<BusinessRuleException>("Different key cannot reuse an already-recorded source", credit with { OperationKey = "credit-source-reuse" });
        foreach (var invalid in new[]
        {
            credit with { Amount = 0 }, credit with { Amount = -1 }, credit with { Amount = 1.5m },
            credit with { Amount = 1000000000 }, credit with { OperationKey = new string('x', 101) },
            credit with { OperationKey = " key-with-space" }, credit with { Actor = new WalletActor("Staff") },
            credit with { Actor = new WalletActor("System", employeeId) },
            credit with { Kind = WalletMutationKind.Debit }, credit with { OriginalDebitId = 1 }
        })
            await Refused<ValidationException>("Writer rejects invalid amount/key/actor/source metadata", invalid);
        await Snapshot("Rejected wallet writes leave no partial changes", customerId, 100000, 1);

        var debit = Mutation(customerId, WalletMutationKind.Debit, 60000, "debit-1");
        var debited = await Commit(debit);
        Check("Debit is stored as a negative amount with its resulting balance", debited.Entry.Amount == -60000
            && debited.Entry.BalanceAfter == 40000 && debited.Entry.ActorEmployeeId == employeeId);
        await Snapshot("Debit commits wallet and ledger together", customerId, 40000, 2);
        await Refused<BusinessRuleException>("Debit cannot overdraw the wallet", Mutation(customerId, WalletMutationKind.Debit, 40001, "debit-too-much"));
        await Snapshot("Insufficient balance cannot partially persist a debit", customerId, 40000, 2);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var added = await writer.RecordAsync(Mutation(customerId, WalletMutationKind.Credit, 50000, "rolled-back-credit"), CancellationToken.None);
            db.Orders.Add(new Order { CustomerId = customerId, EmployeeId = employeeId, TotalAmount = 40000 });
            await db.SaveChangesAsync();
            Check("Caller can flush wallet and business source before committing", added.Entry.TransactionId > 0);
            await transaction.RollbackAsync();
        }
        await Snapshot("Outer rollback removes even a flushed credit and ledger", customerId, 40000, 2);
        await using (var scope = services.CreateAsyncScope())
            Check("Outer rollback removes its business source too", !await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Orders.AnyAsync(row => row.CustomerId == customerId));

        var refund = Mutation(customerId, WalletMutationKind.Refund, 20000, "refund-1", debited.Entry.TransactionId);
        var refunded = await Commit(refund);
        Check("Refund links its committed original wallet debit", refunded.Entry.OriginalDebitId == debited.Entry.TransactionId
            && refunded.Entry.Amount == 20000 && refunded.Entry.Type == "OrderRefund");
        await Snapshot("Partial refund changes balance exactly once", customerId, 60000, 3);
        Check("Refund retry returns saved receipt", (await Commit(refund)).Replayed);
        await Refused<BusinessRuleException>("Refund cannot exceed the remaining original debit", Mutation(customerId, WalletMutationKind.Refund, 40001, "refund-too-much", debited.Entry.TransactionId));
        await Refused<BusinessRuleException>("Refund cannot target a different customer's debit", Mutation(otherId, WalletMutationKind.Refund, 1, "refund-wrong-owner", debited.Entry.TransactionId));
        await Refused<BusinessRuleException>("Refund cannot target a credit", Mutation(customerId, WalletMutationKind.Refund, 1, "refund-credit", replay.Entry.TransactionId));
        await Refused<BusinessRuleException>("Refund cannot target a missing debit", Mutation(customerId, WalletMutationKind.Refund, 1, "refund-missing", int.MaxValue));
        await SetStatus(customerId, "Locked");
        await Refused<BusinessRuleException>("Locked customer cannot receive a new credit", Mutation(customerId, WalletMutationKind.Credit, 1, "locked-credit"));
        await Refused<BusinessRuleException>("Locked customer cannot spend", Mutation(customerId, WalletMutationKind.Debit, 1, "locked-debit"));
        var finalRefund = await Commit(Mutation(customerId, WalletMutationKind.Refund, 40000, "refund-locked", debited.Entry.TransactionId));
        Check("Locked customer still receives a valid remaining refund", finalRefund.Entry.BalanceAfter == 100000);
        await Snapshot("Total refunded amount never exceeds its original debit", customerId, 100000, 4);
        await Refused<BusinessRuleException>("Fully refunded debit cannot be refunded under another key", Mutation(customerId, WalletMutationKind.Refund, 1, "refund-after-full", debited.Entry.TransactionId));
        await SetStatus(customerId, "Active");

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var original = await writer.RecordAsync(Mutation(customerId, WalletMutationKind.Debit, 10000, "uncommitted-debit"), CancellationToken.None);
            await Throws<BusinessRuleException>("An unflushed debit cannot authorize a refund", () => writer.RecordAsync(
                Mutation(customerId, WalletMutationKind.Refund, 1, "refund-unflushed", original.Entry.TransactionId), CancellationToken.None),
                allowValidation: true);
            await db.SaveChangesAsync();
            await Throws<BusinessRuleException>("A flushed but uncommitted debit cannot authorize a refund", () => writer.RecordAsync(
                Mutation(customerId, WalletMutationKind.Refund, 1, "refund-uncommitted", original.Entry.TransactionId), CancellationToken.None));
            await transaction.RollbackAsync();
        }
        await Snapshot("Uncommitted debit and rejected refund roll back together", customerId, 100000, 4);

        await CheckSourceAndPaymentBoundary();
        await CheckRaces();
        await CheckTrackedRefundsAndReservations();
        await CheckCollationAndOverflow();
        return checks;

        WalletMutation Mutation(int id, WalletMutationKind kind, decimal amount, string key, int? original = null) =>
            new(id, kind, amount, kind == WalletMutationKind.Credit ? "TopUp" : kind == WalletMutationKind.Debit ? "OrderPayment" : "OrderRefund",
                kind == WalletMutationKind.Credit ? "CounterTopUp" : kind == WalletMutationKind.Debit ? "Order" : "OrderDetail",
                key, "WriterCheck:" + key, actor, now, original);
        async Task<WalletMutationResult> Commit(WalletMutation mutation)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>().RecordAsync(mutation, CancellationToken.None);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        async Task Refused<T>(string name, WalletMutation mutation) where T : Exception
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await Throws<T>(name, () => scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>().RecordAsync(mutation, CancellationToken.None));
            await transaction.RollbackAsync();
        }
        async Task CheckSourceAndPaymentBoundary()
        {
            var id = await NewCustomer("0780000103", 100000);
            int orderId;
            int ledgerId;
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var writer = scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                await writer.LockCustomerAsync(id, CancellationToken.None);
                var order = new Order { CustomerId = id, EmployeeId = employeeId, TotalAmount = 30000 };
                db.Orders.Add(order);
                await db.SaveChangesAsync();
                var result = await writer.RecordAsync(Mutation(id, WalletMutationKind.Debit, 30000, "integration-order")
                    with { SourceId = order.OrderId.ToString(), OperationKey = "OrderPayment:" + order.OrderId }, CancellationToken.None);
                var receipt = new Payment { Order = order, WalletTransaction = result.Entry, Amount = 30000,
                    Method = "Wallet", Status = "Succeeded", OperationKey = "Payment:WriterOrder:" + order.OrderId,
                    PaymentTime = now, ConfirmedAt = now, PaymentTimeBasis = "Confirmation" };
                db.Payments.Add(receipt);
                await db.SaveChangesAsync();
                orderId = order.OrderId;
                ledgerId = result.Entry.TransactionId;
                await transaction.CommitAsync();
            }
            await using var verify = services.CreateAsyncScope();
            var saved = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("Order source, debit, balance and Payment commit in one caller transaction", await saved.Orders.AnyAsync(row => row.OrderId == orderId)
                && await saved.Customers.Where(row => row.CustomerId == id).Select(row => row.WalletBalance).SingleAsync() == 70000
                && await saved.Payments.AnyAsync(row => row.OrderId == orderId && row.WalletTransactionId == ledgerId
                    && row.Method == "Wallet" && row.Status == "Succeeded" && row.Amount == 30000));
        }
        async Task CheckRaces()
        {
            var id = await NewCustomer("0780000104", 100000);
            var debitResults = await Task.WhenAll(TryCommit(Mutation(id, WalletMutationKind.Debit, 70000, "race-debit-a")),
                TryCommit(Mutation(id, WalletMutationKind.Debit, 70000, "race-debit-b")));
            Check("Concurrent independent debits cannot overdraw one wallet", debitResults.Count(result => result is not null) == 1);
            await Snapshot("Debit race leaves one committed debit and nonnegative balance", id, 30000, 1);
            var originalId = debitResults.Single(result => result is not null)!.Entry.TransactionId;
            var refundResults = await Task.WhenAll(TryCommit(Mutation(id, WalletMutationKind.Refund, 50000, "race-refund-a", originalId)),
                TryCommit(Mutation(id, WalletMutationKind.Refund, 50000, "race-refund-b", originalId)));
            Check("Concurrent independent refunds cannot exceed one debit", refundResults.Count(result => result is not null) == 1);
            await Snapshot("Refund race preserves exactly one partial refund", id, 80000, 2);
            var same = Mutation(id, WalletMutationKind.Credit, 10000, "race-same-credit");
            var sameResults = await Task.WhenAll(Commit(same), Commit(same));
            Check("Concurrent same-key writer calls reuse one committed entry", sameResults.Count(result => result.Replayed) == 1
                && sameResults[0].Entry.TransactionId == sameResults[1].Entry.TransactionId);
            await Snapshot("Same-key writer race credits exactly once", id, 90000, 3);
            static async Task<WalletMutationResult?> Try(Task<WalletMutationResult> operation)
            {
                try { return await operation; }
                catch (BusinessRuleException) { return null; }
            }
            Task<WalletMutationResult?> TryCommit(WalletMutation mutation) => Try(Commit(mutation));
        }
        async Task CheckTrackedRefundsAndReservations()
        {
            var id = await NewCustomer("0780000105", 100000);
            var original = await Commit(Mutation(id, WalletMutationKind.Debit, 100000, "tracked-original"));
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var writer = scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                await writer.RecordAsync(Mutation(id, WalletMutationKind.Refund, 60000, "tracked-refund-a", original.Entry.TransactionId), CancellationToken.None);
                await Throws<BusinessRuleException>("Unflushed refunds count toward the original debit limit", () => writer.RecordAsync(
                    Mutation(id, WalletMutationKind.Refund, 50000, "tracked-refund-b", original.Entry.TransactionId), CancellationToken.None));
                await transaction.RollbackAsync();
            }
            await Snapshot("Rolled-back unflushed refunds do not persist", id, 0, 1);
            int paymentId;
            int orderId;
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var order = new Order { CustomerId = id, EmployeeId = employeeId, TotalAmount = 100000 };
                var payment = new Payment { Order = order, WalletTransactionId = original.Entry.TransactionId, Method = "Wallet",
                    Status = "Succeeded", Amount = 100000, OperationKey = "WriterCheck:reserved-payment", PaymentTime = now,
                    ConfirmedAt = now, PaymentTimeBasis = "Confirmation" };
                db.Payments.Add(payment);
                await db.SaveChangesAsync();
                orderId = order.OrderId;
                paymentId = payment.PaymentId;
                db.Payments.Add(new Payment { OrderId = orderId, Type = "Refund", Method = "Wallet", Status = "Pending", Amount = 70000,
                    OperationKey = "WriterCheck:pending-refund", RefundOfPaymentId = paymentId, Reason = "Reserved wallet refund" });
                await db.SaveChangesAsync();
            }
            await Refused<BusinessRuleException>("Persisted Pending refund reservations count toward the original debit", Mutation(id,
                WalletMutationKind.Refund, 40000, "reservation-overflow", original.Entry.TransactionId));
            var partial = await Commit(Mutation(id, WalletMutationKind.Refund, 30000, "reservation-remaining", original.Entry.TransactionId));
            Check("Refund may use only the amount outside existing reservations", partial.Entry.Amount == 30000);
            await Snapshot("Reservations prevent over-refund without crediting themselves", id, 30000, 2);
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var writer = scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                await writer.LockCustomerAsync(id, CancellationToken.None);
                var reserved = await db.Payments.SingleAsync(row => row.OperationKey == "WriterCheck:pending-refund");
                var completed = await writer.RecordAsync(Mutation(id, WalletMutationKind.Refund, 70000,
                    "reserved-completion", original.Entry.TransactionId) with { OperationKey = reserved.OperationKey }, CancellationToken.None);
                reserved.WalletTransaction = completed.Entry;
                reserved.Status = "Succeeded";
                reserved.PaymentTime = now;
                reserved.ConfirmedAt = now;
                reserved.PaymentTimeBasis = "Confirmation";
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                Check("A refund converts its own Pending reservation without counting it twice", completed.Entry.Amount == 70000);
            }
            await Snapshot("Reservation completion credits the total original debit only once", id, 100000, 3);
        }
        async Task CheckCollationAndOverflow()
        {
            var id = await NewCustomer("0780000106");
            await Commit(Mutation(id, WalletMutationKind.Credit, 1, "CaseKey") with { OperationKey = "WriterCheck:CaseKey" });
            await Commit(Mutation(id, WalletMutationKind.Credit, 1, "casekey") with { OperationKey = "WriterCheck:casekey" });
            await Snapshot("Binary SQL operation keys preserve case-distinct trusted sources", id, 2, 2);
            var full = await NewCustomer("0780000107", 999999999999999999m);
            await Refused<BusinessRuleException>("Credit cannot exceed decimal18 storage capacity", Mutation(full, WalletMutationKind.Credit, 1, "overflow-credit"));
            await Snapshot("Overflow rejection does not create a ledger", full, 999999999999999999m, 0);
            var systemId = await NewCustomer("0780000108", 50000);
            var systemCharge = Mutation(systemId, WalletMutationKind.Debit, 10000, "system-charge") with
            { Type = "GamingCharge", SourceType = "SessionCharge", Actor = new WalletActor("System") };
            var result = await Commit(systemCharge);
            Check("Internal session settlement can record its System audit actor", result.Entry.ActorType == "System"
                && result.Entry.ActorEmployeeId is null && result.Entry.Amount == -10000);
        }
        async Task Snapshot(string name, int id, decimal balance, int entries)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check(name, await db.Customers.Where(row => row.CustomerId == id).Select(row => row.WalletBalance).SingleAsync() == balance
                && await db.WalletTransactions.CountAsync(row => row.CustomerId == id) == entries);
        }
        async Task<int> NewCustomer(string phone, decimal initialBalance = 0)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customer = new Customer { FullName = "SQL wallet writer " + phone, Phone = phone, WalletBalance = initialBalance, Status = "Active" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.CustomerId;
        }
        async Task<int> StaffId()
        {
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Employees.Where(row => row.Username == "admin")
                .Select(row => row.EmployeeId).SingleAsync();
        }
        async Task SetStatus(int id, string status)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Customers.SingleAsync(row => row.CustomerId == id)).Status = status;
            await db.SaveChangesAsync();
        }
        async Task Throws<T>(string name, Func<Task> operation, bool allowValidation = false) where T : Exception
        {
            try { await operation(); }
            catch (T) { Check(name, true); return; }
            catch (ValidationException) when (allowValidation) { Check(name, true); return; }
            throw new InvalidOperationException(name + ": expected exception " + typeof(T).Name);
        }
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }
    }
}
