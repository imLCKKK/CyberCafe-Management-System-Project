using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class PromotionUsage //Ghi nhận lượt dùng voucher
    {
        public long PromotionUsageId { get; set; }

        public long PromotionId { get; set; }
        public long OrderId { get; set; }
        public long CustomerId { get; set; }

        public DateTimeOffset UsedAt { get; set; }

        // Có giá trị khi hủy toàn bộ đơn và hoàn lượt sử dụng.
        public DateTimeOffset? ReleasedAt { get; set; }

        public Promotions Promotion { get; set; } = null!;
        public Order Order { get; set; } = null!;
        public Customers Customer { get; set; } = null!;
    }
}
