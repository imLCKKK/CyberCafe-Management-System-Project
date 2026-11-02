using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Infrastructure.Persistance;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Repositories
{
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly ApplicationDbContext _context;

        public InvoiceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Lấy tối đa 500 hóa đơn mới nhất cho Admin/Staff.
        public async Task<IReadOnlyList<InvoiceResponse>> GetRecentAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Invoices
                .AsNoTracking()
                .OrderByDescending(invoice => invoice.IssuedAt)
                .ThenByDescending(invoice => invoice.InvoiceId)
                .Take(500)
                .Select(invoice => new InvoiceResponse
                {
                    Id = invoice.InvoiceId,
                    CustomerId = invoice.CustomerId,
                    OrderId = invoice.OrderId,
                    GamingSessionId = invoice.SessionId,
                    GrossAmount = invoice.GrossAmount,
                    RefundAmount = invoice.RefundAmount,
                    PaidAmount = invoice.PaidAmount,
                    Status = invoice.Status,
                    IssuedAt = invoice.IssuedAt
                })
                .ToListAsync(cancellationToken);
        }

        // Chỉ lấy hóa đơn thuộc khách được chỉ định.
        public async Task<IReadOnlyList<InvoiceResponse>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(customerId);

            return await _context.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.CustomerId == customerId)
                .OrderByDescending(invoice => invoice.IssuedAt)
                .ThenByDescending(invoice => invoice.InvoiceId)
                .Take(200)
                .Select(invoice => new InvoiceResponse
                {
                    Id = invoice.InvoiceId,
                    CustomerId = invoice.CustomerId,
                    OrderId = invoice.OrderId,
                    GamingSessionId = invoice.SessionId,
                    GrossAmount = invoice.GrossAmount,
                    RefundAmount = invoice.RefundAmount,
                    PaidAmount = invoice.PaidAmount,
                    Status = invoice.Status,
                    IssuedAt = invoice.IssuedAt
                })
                .ToListAsync(cancellationToken);
        }
    }
}
