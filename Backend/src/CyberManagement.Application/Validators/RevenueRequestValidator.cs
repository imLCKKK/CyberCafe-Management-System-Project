using CyberManagement.Application.DTOs.Revenue;
using CyberManagement.Application.Interfaces.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CyberManagement.Application.Validators
{
    public sealed class RevenueRequestValidator : IRequestValidator<RevenueFilterRequest>
    {
        private const int MaxRangeDays = 366;

        public void Validate(RevenueFilterRequest request)
        {
            if (request is null)
            {
                throw new ValidationException("Vui lòng cung cấp khoảng ngày báo cáo.");
            }

            // DateOnly mặc định nghĩa là chưa cung cấp ngày.
            if (request.From == default || request.To == default)
            {
                throw new ValidationException("Ngày bắt đầu và ngày kết thúc là bắt buộc.");
            }

            if (request.From > request.To)
            {
                throw new ValidationException("Ngày bắt đầu không được sau ngày kết thúc.");
            }

            // Service cần lấy đầu ngày kế tiếp làm mốc kết thúc.
            if (request.To == DateOnly.MaxValue)
            {
                throw new ValidationException("Ngày kết thúc vượt phạm vi được hỗ trợ.");
            }

            // Tính cả ngày bắt đầu và ngày kết thúc.
            var totalDays = request.To.DayNumber - request.From.DayNumber + 1;

            if (totalDays > MaxRangeDays)
            {
                throw new ValidationException($"Khoảng ngày báo cáo không được vượt quá {MaxRangeDays} ngày.");
            }
        }
    }
}
