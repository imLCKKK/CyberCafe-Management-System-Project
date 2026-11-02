using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Invoice
    {
        public long InvoiceId { get; set; }
        public long CustomerId { get; set; }

        // Nguồn hóa đơn: một đơn món ăn hoặc một phiên chơi.
        // SessionId đã có sẵn, không thêm lại.
        public long? OrderId { get; set; }
        public long? SessionId { get; set; }
        
        public long? EmployeeId { get; set; }
        public long? PromotionId { get; set; }

        // Giá trị sau voucher tại lúc phát hành, trước khi hủy.
        public decimal GrossAmount { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }

        // Giá trị giảm do hủy.
        public decimal RefundAmount { get; set; }

        // Tiền thực thanh toán ròng sau hoàn tiền.
        public decimal PaidAmount { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Paid, Cancelled
        public DateTimeOffset IssuedAt { get; set; }

        public virtual Order? Order { get; set; }
        public virtual GamingSessions? Session { get; set; }
        public virtual Customers Customer { get; set; } = null!;
        public virtual Employees? Employee { get; set; }
        public virtual Promotions? Promotion { get; set; }

        public virtual ICollection<Payment> Payments { get; set; }
            = new List<Payment>();

        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; }
            = new List<InvoiceDetail>();
    }
}
