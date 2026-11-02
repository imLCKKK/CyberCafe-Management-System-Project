using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    //DiscountType = "Percent";
    //DiscountValue = 20(%);
    //MinOrderAmount = 100000;
    //MaxDiscountAmount = null;
    public class Promotions
    {
        public long PromotionId { get; set; }

        public string Code { get; set; } = string.Empty; // Mã khách nhập, ví dụ FOOD20
        public string PromotionName { get; set; } = string.Empty;

        // Percent: phần trăm; Fixed: số tiền cố định.
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }

        public decimal MinOrderAmount { get; set; } // Tổng tiền món tối thiểu trước giảm; 0 là không yêu cầu ngưỡng
        public decimal? MaxDiscountAmount { get; set; } // Mức giảm tối đa; null là không đặt trần, nhưng giảm vẫn không vượt tiền món

        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }

        public int? TotalUsageLimit { get; set; } // Giới hạn lượt dùng toàn hệ thống; null là không giới hạn
        public int? PerCustomerUsageLimit { get; set; } // Giới hạn lượt dùng mỗi khách; null là không giới hạn

        public bool IsActive { get; set; } = true;

    }
}
