using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepository;
        public InvoiceService(IInvoiceRepository invoiceRepository)
        {
            _invoiceRepository = invoiceRepository;
        }

        private InvoiceDTOs MapToDTO(Invoice invoice)
        {
            return new InvoiceDTOs
            {
                InvoiceId = invoice.InvoiceId,
                OrderId = invoice.OrderId,
                TotalAmount = invoice.TotalAmount,
                PaymentTime = invoice.PaymentTime,
                PaymentMethod = invoice.Payments.FirstOrDefault()?.Method ?? string.Empty
            };
        }

        public async Task<IEnumerable<InvoiceDTOs>> GetAllAsync()
        {
            var invoices = await _invoiceRepository.GetAllAsync();
            return invoices.Select(MapToDTO).ToList();
        }

        public async Task<InvoiceDTOs?> GetByIdAsync(int id)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(id);
            if (invoice == null) return null;

            return MapToDTO(invoice);
        }

        public async Task<InvoiceDTOs?> GetByOrderIdAsync(int orderId)
        {
            var invoice = await _invoiceRepository.GetByOrderIdAsync(orderId);
            if (invoice == null) return null;

            return MapToDTO(invoice);
        }
    }
}
