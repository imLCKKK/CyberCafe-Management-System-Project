using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

public sealed record InvoiceSources(Customer Customer, GamingSession? Session,
    IReadOnlyList<Order> Orders, IReadOnlyList<SessionCharge> Charges,
    IReadOnlyList<Payment> Payments, IReadOnlyList<WalletTransaction> Ledgers,
    IReadOnlyList<int> BilledOrderIds, IReadOnlyList<int> BilledChargeIds);

public sealed record InvoiceReadSet(IReadOnlyList<Invoice> Invoices,
    IReadOnlyList<Payment> Payments, IReadOnlyList<WalletTransaction> Ledgers, IReadOnlyList<int> CancelledDetailIds);

// Các thao tác ghi đều dùng DbContext/transaction chung của caller.
public interface IInvoiceRepository
{
    Task<Employee?> GetEmployeeAsync(int employeeId, CancellationToken ct);
    Task<(int CustomerId, int? SessionId)> IdentifySourceAsync(IReadOnlyList<int> orderIds, int? sessionId, CancellationToken ct);
    Task<InvoiceSources> LockSourcesAsync(int customerId, int? sessionId, IReadOnlyList<int> orderIds, CancellationToken ct);
    Task<InvoiceIssueRequest?> GetIssueRequestAsync(string key, CancellationToken ct);
    Task<InvoiceReadSet> ReadAsync(int? customerId, int? invoiceId, CancellationToken ct);
    Task<Invoice?> LockInvoiceAsync(int invoiceId, CancellationToken ct);
    Task AddAsync(Invoice invoice, InvoiceIssueRequest request, AuditLog audit, CancellationToken ct);
    Task<int?> FindInvoiceForSourceAsync(int? orderId, int? chargeId, CancellationToken ct);
}
