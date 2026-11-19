using CyberManagement.Application.DTOs.Vouchers;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Validators
{
    internal static class VoucherValidator
    {
        // Giá trị lớn nhất của decimal(18, 0) trong database.
        private const decimal MaxMoney = 999999999999999999m;

        // Chuẩn hóa mã: bỏ khoảng trắng hai đầu và chuyển thành chữ hoa.
        public static string NormalizeCode(string? code)
        {
            var value = code?.Trim();

            if (string.IsNullOrEmpty(value) || value.Length > 50)
            {
                throw new ArgumentException(
                    "Mã voucher phải có từ 1 đến 50 ký tự.");
            }

            foreach (var character in value)
            {
                var valid = character is
                    >= 'A' and <= 'Z'
                    or >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-' or '_';

                if (!valid)
                {
                    throw new ArgumentException(
                        "Mã voucher chỉ được chứa chữ không dấu, số, - và _.");
                }
            }

            return value.ToUpperInvariant();
        }

        // Kiểm tra dữ liệu tạo/sửa và trả về bản đã chuẩn hóa.
        public static SaveVoucherRequest NormalizeAndValidate(SaveVoucherRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var code = NormalizeCode(request.Code);
            var name = request.Name?.Trim();

            if (string.IsNullOrEmpty(name) || name.Length > 150)
            {
                throw new ArgumentException(
                    "Tên voucher phải có từ 1 đến 150 ký tự.");
            }

            var discountType = request.DiscountType?
                .Trim()
                .ToUpperInvariant() switch
            {
                "PERCENT" => "Percent",
                "FIXED" => "Fixed",
                _ => throw new ArgumentException(
                    "Loại giảm phải là Percent hoặc Fixed.")
            };

            if (discountType == "Percent")
            {
                if (request.DiscountValue <= 0
                    || request.DiscountValue > 100
                    || decimal.Round(request.DiscountValue, 2)
                        != request.DiscountValue)
                {
                    throw new ArgumentException(
                        "Phần trăm giảm phải lớn hơn 0, không quá 100 "
                        + "và có tối đa 2 chữ số thập phân.");
                }
            }
            else
            {
                // DiscountValue được lưu bằng decimal(18, 2).
                ValidateMoney(
                    request.DiscountValue,
                    "Tiền giảm cố định",
                    allowZero: false);

                if (request.DiscountValue > 9999999999999999m)
                {
                    throw new ArgumentException(
                        "Tiền giảm cố định vượt giới hạn lưu trữ.");
                }
            }

            ValidateMoney(
                request.MinOrderAmount,
                "Ngưỡng đơn tối thiểu",
                allowZero: true);

            if (request.MaxDiscountAmount is decimal maximum)
            {
                ValidateMoney(
                    maximum,
                    "Trần giảm",
                    allowZero: false);
            }

            if (request.StartDate >= request.EndDate)
            {
                throw new ArgumentException(
                    "Ngày bắt đầu phải trước ngày kết thúc.");
            }

            if (request.TotalUsageLimit is <= 0)
            {
                throw new ArgumentException(
                    "Giới hạn lượt toàn hệ thống phải lớn hơn 0 hoặc null.");
            }

            if (request.PerCustomerUsageLimit is <= 0)
            {
                throw new ArgumentException(
                    "Giới hạn lượt mỗi khách phải lớn hơn 0 hoặc null.");
            }

            return request with
            {
                Code = code,
                Name = name,
                DiscountType = discountType,
                StartDate = request.StartDate.ToUniversalTime(),
                EndDate = request.EndDate.ToUniversalTime()
            };
        }

        // Kiểm tra tiền VND: nguyên đồng và nằm trong giới hạn lưu trữ.
        public static void ValidateMoney(decimal amount, string fieldName, bool allowZero)
        {
            if (amount < 0
                || (!allowZero && amount == 0)
                || amount > MaxMoney
                || decimal.Truncate(amount) != amount)
            {
                throw new ArgumentException(
                    $"{fieldName} phải là số nguyên "
                    + (allowZero ? "không âm" : "lớn hơn 0")
                    + " và không vượt giới hạn lưu trữ.");
            }
        }
    }
}
