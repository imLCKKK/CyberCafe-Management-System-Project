using System.Numerics;
using CyberManagement.Application.DTOs.Vouchers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Validators;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

// Quản lý voucher và cung cấp rule dùng chung cho giỏ hàng, đặt và hủy đơn.
public sealed class VoucherService(
    IVoucherRepository voucherRepository,
    IPromotionUsageRepository usageRepository,
    IVoucherUnitOfWork unitOfWork) : IVoucherService
{
    private const decimal MaxMoney = 999999999999999999m;

    // Đọc danh sách, không ghi dữ liệu.
    public Task<IReadOnlyList<VoucherDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        voucherRepository.GetAllAsync(cancellationToken);

    public async Task<VoucherDto> GetByIdAsync(int promotionId, CancellationToken cancellationToken = default)
    {
        ValidateId(promotionId, "Voucher");
        var promotion = await voucherRepository.GetByIdAsync(promotionId, cancellationToken)
            ?? throw NotFound(promotionId);
        return ToDto(promotion);
    }

    // Unique index vẫn chặn được hai yêu cầu cùng tạo một mã.
    public Task<VoucherDto> CreateAsync(SaveVoucherRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = VoucherValidator.NormalizeAndValidate(request);
        return unitOfWork.ExecuteAsync(async () =>
        {
            if (await voucherRepository.CodeExistsAsync(normalized.Code, cancellationToken: cancellationToken))
                throw new BusinessRuleException($"Mã voucher {normalized.Code} đã tồn tại.");

            var promotion = new Promotion();
            ApplyRequest(promotion, normalized);
            await voucherRepository.AddAsync(promotion, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ToDto(promotion);
        }, cancellationToken);
    }

    // Khóa voucher trước khi sửa; không đổi mã đã từng có lượt dùng.
    public Task<VoucherDto> UpdateAsync(int promotionId, SaveVoucherRequest request, CancellationToken cancellationToken = default)
    {
        ValidateId(promotionId, "Voucher");
        var normalized = VoucherValidator.NormalizeAndValidate(request);
        return unitOfWork.ExecuteAsync(async () =>
        {
            var promotion = await voucherRepository.GetByIdForUpdateAsync(promotionId, cancellationToken)
                ?? throw NotFound(promotionId);
            if (promotion.Code != normalized.Code
                && await usageRepository.HasHistoryAsync(promotionId, cancellationToken))
                throw new BusinessRuleException("Không được đổi mã voucher đã có lịch sử sử dụng.");
            if (await voucherRepository.CodeExistsAsync(normalized.Code, promotionId, cancellationToken))
                throw new BusinessRuleException($"Mã voucher {normalized.Code} đã tồn tại.");

            ApplyRequest(promotion, normalized);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ToDto(promotion);
        }, cancellationToken);
    }

    // Bật/tắt chỉ ảnh hưởng đơn mới, không thay snapshot đã lưu.
    public Task<VoucherDto> UpdateStatusAsync(int promotionId, UpdateVoucherStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateId(promotionId, "Voucher");
        ArgumentNullException.ThrowIfNull(request);
        return unitOfWork.ExecuteAsync(async () =>
        {
            var promotion = await voucherRepository.GetByIdForUpdateAsync(promotionId, cancellationToken)
                ?? throw NotFound(promotionId);
            promotion.IsActive = request.IsActive;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ToDto(promotion);
        }, cancellationToken);
    }

    // Tính thử bằng giá server; không tạo đơn, ghi lượt hoặc giữ chỗ.
    public async Task<VoucherPreviewDto> PreviewAsync(PreviewVoucherRequest request, int customerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(customerId, "Khách hàng");
        var code = VoucherValidator.NormalizeCode(request.Code);
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Giỏ hàng phải có ít nhất một món.");
        if (request.Items.Any(x => x is null || x.ProductId <= 0 || x.Quantity <= 0))
            throw new ArgumentException("ID sản phẩm và số lượng phải lớn hơn 0.");

        var ids = request.Items.Select(x => x.ProductId).Distinct().ToArray();
        var products = (await voucherRepository.GetProductsForPreviewAsync(ids, cancellationToken))
            .ToDictionary(x => x.ProductId);
        decimal subtotal = 0;
        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new EntityNotFoundException($"Không tìm thấy sản phẩm có ID {item.ProductId}.");
            if (!product.IsActive || product.Category is null || !product.Category.IsActive)
                throw new BusinessRuleException($"Sản phẩm {item.ProductId} đang ngừng bán.");
            subtotal = AddLine(subtotal, product.Price, item.Quantity);
        }

        var promotion = await voucherRepository.GetByCodeAsync(code, cancellationToken)
            ?? throw new EntityNotFoundException($"Không tìm thấy voucher {code}.");
        var now = DateTimeOffset.UtcNow;
        await ValidateAvailabilityAsync(promotion, customerId, subtotal, now, cancellationToken);
        var discount = CalculateDiscount(promotion, subtotal);
        return new VoucherPreviewDto(promotion.Code, subtotal, discount, subtotal - discount);
    }

    // Caller đã lưu đơn/dòng món và giữ khóa nguồn trong cùng transaction.
    // Lưu usage trước khi trả về để lần gọi tiếp theo trong transaction đếm đúng lượt.
    public async Task<VoucherApplicationResult> ValidateAndUseAsync(int orderId, string code, int customerId,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        ValidateId(orderId, "Đơn hàng");
        ValidateId(customerId, "Khách hàng");
        var normalizedCode = VoucherValidator.NormalizeCode(code);
        var order = await voucherRepository.GetOrderForUpdateAsync(orderId, cancellationToken)
            ?? throw new EntityNotFoundException($"Không tìm thấy đơn có ID {orderId}.");
        if (order.CustomerId != customerId)
            throw new UnauthorizedAccessException("Đơn hàng không thuộc khách hàng này.");

        var existing = await usageRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ReleasedAt is not null || order.Status == "Cancelled")
                throw new BusinessRuleException("Đơn đã hoàn lượt hoặc đã hủy, không thể áp lại voucher.");
            if (existing.CustomerId != customerId || order.VoucherCodeSnapshot != normalizedCode)
                throw new BusinessRuleException("Đơn đã sử dụng một voucher khác.");
            // Retry đọc snapshot cũ, kể cả voucher hiện đã sửa hoặc hết hạn.
            return ReadApplicationResult(order, existing.PromotionId);
        }

        if (order.Status != "Pending")
            throw new BusinessRuleException("Chỉ áp voucher khi tạo đơn đang chờ xử lý.");
        if (order.VoucherCodeSnapshot is not null || order.VoucherDiscountTypeSnapshot is not null
            || order.VoucherDiscountValueSnapshot is not null || order.DiscountAmount != 0
            || order.Details.Any(x => x.DiscountAmount != 0))
            throw new BusinessRuleException("Đơn đã có snapshot giảm giá, không được áp thêm voucher.");

        var subtotal = GetSubtotal(order);
        var promotion = await voucherRepository.GetByCodeForUpdateAsync(normalizedCode, cancellationToken)
            ?? throw new EntityNotFoundException($"Không tìm thấy voucher {normalizedCode}.");
        var now = DateTimeOffset.UtcNow;
        await ValidateAvailabilityAsync(promotion, customerId, subtotal, now, cancellationToken);
        var discount = CalculateDiscount(promotion, subtotal);
        var allocations = AllocateDiscount(order, subtotal, discount);
        foreach (var detail in order.Details)
            detail.DiscountAmount = allocations[detail.OrderDetailId];

        order.VoucherCodeSnapshot = promotion.Code;
        order.VoucherDiscountTypeSnapshot = promotion.DiscountType;
        order.VoucherDiscountValueSnapshot = promotion.DiscountValue;
        order.DiscountAmount = discount;
        order.TotalAmount = subtotal - discount;
        await usageRepository.AddAsync(new PromotionUsage
        {
            PromotionId = promotion.PromotionId,
            CustomerId = customerId,
            OrderId = orderId,
            UsedAt = now
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ReadApplicationResult(order, promotion.PromotionId);
    }

    // Hủy toàn đơn mới hoàn lượt; giữ nguyên snapshot và lịch sử.
    public async Task<bool> ReleaseForCancelledOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        ValidateId(orderId, "Đơn hàng");
        var order = await voucherRepository.GetOrderForUpdateAsync(orderId, cancellationToken)
            ?? throw new EntityNotFoundException($"Không tìm thấy đơn có ID {orderId}.");
        if (order.Status != "Cancelled")
            throw new BusinessRuleException("Chỉ hoàn lượt voucher khi đơn đã hủy toàn bộ.");
        var usage = await usageRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (usage is null || usage.ReleasedAt is not null) return false;
        if (usage.CustomerId != order.CustomerId)
            throw new BusinessRuleException("Lượt voucher không khớp khách hàng của đơn.");
        _ = await voucherRepository.GetByIdForUpdateAsync(usage.PromotionId, cancellationToken)
            ?? throw NotFound(usage.PromotionId);
        var now = DateTimeOffset.UtcNow;
        return await usageRepository.ReleaseAsync(usage.PromotionUsageId,
            now < usage.UsedAt ? usage.UsedAt : now, cancellationToken);
    }

    // Với thao tác ghi lượt, caller phải giữ khóa voucher khi kiểm tra hạn mức.
    private async Task ValidateAvailabilityAsync(Promotion promotion, int customerId, decimal subtotal,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!promotion.IsActive) throw new BusinessRuleException("Voucher đang tắt.");
        if (now < promotion.StartDate) throw new BusinessRuleException("Voucher chưa đến ngày hiệu lực.");
        if (now >= promotion.EndDate) throw new BusinessRuleException("Voucher đã hết hạn.");
        if (subtotal < promotion.MinOrderAmount)
            throw new BusinessRuleException("Đơn chưa đạt ngưỡng tối thiểu của voucher.");
        if (promotion.TotalUsageLimit is int totalLimit
            && await usageRepository.CountActiveAsync(promotion.PromotionId, cancellationToken) >= totalLimit)
            throw new BusinessRuleException("Voucher đã hết lượt sử dụng toàn hệ thống.");
        if (promotion.PerCustomerUsageLimit is int customerLimit
            && await usageRepository.CountActiveByCustomerAsync(promotion.PromotionId, customerId, cancellationToken) >= customerLimit)
            throw new BusinessRuleException("Khách hàng đã dùng hết lượt của voucher này.");
    }

    // Tiền giảm nguyên đồng, không vượt trần hoặc tổng tiền món.
    private static decimal CalculateDiscount(Promotion promotion, decimal subtotal)
    {
        VoucherValidator.ValidateMoney(subtotal, "Tổng tiền món", allowZero: true);
        var discount = promotion.DiscountType switch
        {
            "Percent" => subtotal * (promotion.DiscountValue / 100m),
            "Fixed" => promotion.DiscountValue,
            _ => throw new InvalidOperationException("Loại giảm trong dữ liệu voucher không hợp lệ.")
        };
        if (promotion.MaxDiscountAmount is decimal maximum) discount = Math.Min(discount, maximum);
        discount = Math.Clamp(discount, 0m, subtotal);
        return Math.Clamp(decimal.Round(discount, 0, MidpointRounding.AwayFromZero), 0m, subtotal);
    }

    // Chia nguyên đồng theo tỷ lệ; phần dư ưu tiên phần lẻ lớn, hòa theo ID dòng.
    // BigInteger giữ phép chia chính xác khi tiền lớn, không tràn phép nhân decimal.
    private static Dictionary<int, decimal> AllocateDiscount(Order order, decimal subtotal, decimal discount)
    {
        var result = order.Details.ToDictionary(x => x.OrderDetailId, _ => 0m);
        if (subtotal == 0 || discount == 0) return result;
        var remainders = new List<(int Id, BigInteger Remainder)>();
        decimal allocated = 0;
        foreach (var detail in order.Details.OrderBy(x => x.OrderDetailId))
        {
            var gross = detail.UnitPrice * detail.Quantity;
            var whole = BigInteger.DivRem((BigInteger)discount * (BigInteger)gross,
                (BigInteger)subtotal, out var remainder);
            result[detail.OrderDetailId] = (decimal)whole;
            allocated += (decimal)whole;
            remainders.Add((detail.OrderDetailId, remainder));
        }
        var remaining = (int)(discount - allocated);
        foreach (var line in remainders.OrderByDescending(x => x.Remainder).ThenBy(x => x.Id).Take(remaining))
            result[line.Id]++;
        return result;
    }

    private static decimal GetSubtotal(Order order)
    {
        if (order.Details.Count == 0) throw new BusinessRuleException("Đơn hàng không có dòng món.");
        decimal subtotal = 0;
        foreach (var detail in order.Details)
        {
            if (detail.OrderDetailId <= 0 || detail.Quantity <= 0)
                throw new BusinessRuleException("Dòng món phải được lưu và có số lượng hợp lệ.");
            subtotal = AddLine(subtotal, detail.UnitPrice, detail.Quantity);
        }
        return subtotal;
    }

    private static decimal AddLine(decimal subtotal, decimal price, int quantity)
    {
        VoucherValidator.ValidateMoney(price, "Đơn giá", allowZero: true);
        var total = subtotal + price * quantity;
        if (total > MaxMoney) throw new ArgumentException("Tổng tiền món vượt giới hạn lưu trữ.");
        return total;
    }

    // Retry trả dữ liệu lịch sử, không tính lại bằng cấu hình voucher hiện tại.
    private static VoucherApplicationResult ReadApplicationResult(Order order, int promotionId)
    {
        var subtotal = GetSubtotal(order);
        if (order.VoucherCodeSnapshot is null || order.VoucherDiscountTypeSnapshot is null
            || order.VoucherDiscountValueSnapshot is null
            || order.DiscountAmount < 0 || order.DiscountAmount > subtotal
            || order.Details.Any(x => x.DiscountAmount < 0 || x.DiscountAmount > x.UnitPrice * x.Quantity)
            || order.Details.Sum(x => x.DiscountAmount) != order.DiscountAmount)
            throw new BusinessRuleException("Snapshot voucher của đơn không nhất quán.");
        return new VoucherApplicationResult(promotionId, order.VoucherCodeSnapshot,
            order.VoucherDiscountTypeSnapshot, order.VoucherDiscountValueSnapshot.Value,
            subtotal, order.DiscountAmount, subtotal - order.DiscountAmount,
            order.Details.OrderBy(x => x.OrderDetailId)
                .Select(x => new VoucherLineDiscountDto(x.OrderDetailId, x.DiscountAmount)).ToArray());
    }

    private void RequireTransaction()
    {
        if (!unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Phải dùng transaction của đơn hàng khi áp hoặc hoàn lượt voucher.");
    }

    private static void ValidateId(int id, string name)
    {
        if (id <= 0) throw new ArgumentException($"ID {name} phải lớn hơn 0.");
    }

    private static EntityNotFoundException NotFound(int id) => new($"Không tìm thấy voucher có ID {id}.");

    private static void ApplyRequest(Promotion promotion, SaveVoucherRequest request)
    {
        promotion.Code = request.Code;
        promotion.PromoName = request.Name;
        promotion.DiscountType = request.DiscountType;
        promotion.DiscountValue = request.DiscountValue;
        promotion.MinOrderAmount = request.MinOrderAmount;
        promotion.MaxDiscountAmount = request.MaxDiscountAmount;
        promotion.StartDate = request.StartDate;
        promotion.EndDate = request.EndDate;
        promotion.TotalUsageLimit = request.TotalUsageLimit;
        promotion.PerCustomerUsageLimit = request.PerCustomerUsageLimit;
        promotion.IsActive = request.IsActive;
    }

    private static VoucherDto ToDto(Promotion promotion) => new(
        promotion.PromotionId, promotion.Code, promotion.PromoName, promotion.DiscountType,
        promotion.DiscountValue, promotion.MinOrderAmount, promotion.MaxDiscountAmount,
        promotion.StartDate, promotion.EndDate, promotion.TotalUsageLimit,
        promotion.PerCustomerUsageLimit, promotion.IsActive);
}
