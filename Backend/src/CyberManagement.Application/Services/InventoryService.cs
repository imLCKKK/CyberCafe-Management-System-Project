using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CyberManagement.Application.Services
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IInventoryMovementRepository _movementRepository;
        private readonly IInventoryUnitOfWork _unitOfWork;

        public InventoryService(IInventoryRepository inventoryRepository, IInventoryMovementRepository movementRepository, IInventoryUnitOfWork unitOfWork)
        {
            _inventoryRepository = inventoryRepository;
            _movementRepository = movementRepository;
            _unitOfWork = unitOfWork;
        }

        // Đọc lịch sử biến động kho.
        public Task<IReadOnlyList<InventoryMovementDto>> GetMovementsAsync(CancellationToken cancellationToken = default)
        {
            return _movementRepository.GetRecentAsync(cancellationToken);
        }

        // Nhập hoặc điều chỉnh tồn thủ công.
        public async Task<InventoryMovementDto> CreateMovementAsync(CreateInventoryMovementRequest request, int employeeId, CancellationToken cancellationToken = default)
        {
            var normalizedRequest = ValidateRequest(request);

            if (employeeId <= 0)
            {
                throw new ValidationException(
                    "Phải xác định nhân viên thực hiện thao tác kho.");
            }

            return await _unitOfWork.ExecuteAsync(async () =>
            {
                var product =
                    await _inventoryRepository.GetProductForUpdateAsync(
                        normalizedRequest.ProductId,
                        cancellationToken);

                if (product is null)
                {
                    throw new EntityNotFoundException(
                        $"Không tìm thấy sản phẩm có ID {normalizedRequest.ProductId}.");
                }

                // Mỗi thao tác thủ công mới có một khóa riêng.
                var operationKey = $"Manual:{Guid.NewGuid():N}";

                return await ChangeStockAsync(
                    product,
                    normalizedRequest.Type,
                    normalizedRequest.QuantityDelta,
                    normalizedRequest.Reason,
                    employeeId,
                    operationKey,
                    null,
                    cancellationToken);
            }, cancellationToken);
        }

        // Chỉ đổi trạng thái bán, không đổi tồn hoặc giá.
        public async Task<InventoryItemDto> UpdateProductStatusAsync(int productId, UpdateProductStatusRequest request, CancellationToken cancellationToken = default)
        {
            if (productId <= 0)
            {
                throw new ValidationException(
                    "ID sản phẩm phải lớn hơn 0.");
            }

            if (request is null || !request.IsActive.HasValue)
            {
                throw new ValidationException(
                    "Phải cung cấp trạng thái bán của sản phẩm.");
            }

            return await _unitOfWork.ExecuteAsync(async () =>
            {
                var product =
                    await _inventoryRepository.GetProductForUpdateAsync(
                        productId,
                        cancellationToken);

                if (product is null)
                {
                    throw new EntityNotFoundException(
                        $"Không tìm thấy sản phẩm có ID {productId}.");
                }

                product.IsActive = request.IsActive.Value;

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ToItemDto(product);
            }, cancellationToken);
        }

        // Luồng đặt món gọi để trừ tồn.
        public Task<InventoryMovementDto> ApplySaleAsync(
            int orderDetailId,
            int productId,
            int quantity,
            int? employeeId,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            return ApplyOrderMovementAsync(
                orderDetailId,
                productId,
                quantity,
                employeeId,
                operationKey,
                "Sale",
                cancellationToken);
        }

        // Luồng hủy hoặc hết hạn đơn gọi để hoàn tồn.
        public Task<InventoryMovementDto> ApplyReturnAsync(
            int orderDetailId,
            int productId,
            int quantity,
            int? employeeId,
            string operationKey,
            CancellationToken cancellationToken = default)
        {
            return ApplyOrderMovementAsync(
                orderDetailId,
                productId,
                quantity,
                employeeId,
                operationKey,
                "Return",
                cancellationToken);
        }

        // Xử lý chung cho xuất và hoàn tồn theo dòng món.
        private async Task<InventoryMovementDto> ApplyOrderMovementAsync(
            int orderDetailId,
            int productId,
            int quantity,
            int? employeeId,
            string operationKey,
            string type,
            CancellationToken cancellationToken)
        {
            ValidateOrderMovement(
                orderDetailId,
                productId,
                quantity,
                employeeId,
                operationKey);

            // Bên đặt/hủy món phải mở transaction và khóa đơn trước.
            if (!_unitOfWork.HasActiveTransaction)
            {
                throw new InvalidOperationException(
                    "Phải mở transaction của đơn trước khi xuất hoặc hoàn tồn.");
            }

            var detail = await _inventoryRepository.GetOrderDetailAsync(
                orderDetailId,
                cancellationToken);

            if (detail is null)
            {
                throw new EntityNotFoundException(
                    $"Không tìm thấy dòng món có ID {orderDetailId}.");
            }

            // Chỉ xử lý đúng sản phẩm và toàn bộ số lượng của dòng món.
            if (detail.OrderId <= 0
                || detail.ProductId != productId
                || detail.Quantity != quantity)
            {
                throw new BusinessRuleException(
                    "Sản phẩm hoặc số lượng không khớp với dòng món.");
            }

            var product =
                await _inventoryRepository.GetProductForUpdateAsync(
                    productId,
                    cancellationToken);

            if (product is null)
            {
                throw new EntityNotFoundException(
                    $"Không tìm thấy sản phẩm có ID {productId}.");
            }

            var quantityDelta = type == "Sale" ? -quantity : quantity;

            // Kiểm tra sau khi đã lấy khóa sản phẩm.
            var byKey = await _movementRepository.GetByOperationKeyAsync(
                operationKey,
                cancellationToken);

            if (byKey is not null)
            {
                EnsureSameMovement(
                    byKey,
                    detail,
                    type,
                    quantityDelta);

                return ToMovementDto(byKey);
            }

            // Đổi khóa cũng không được xuất hoặc hoàn cùng dòng lần hai.
            var bySource = await _movementRepository.GetByOrderDetailAsync(
                orderDetailId,
                type,
                cancellationToken);

            if (bySource is not null)
            {
                EnsureSameMovement(
                    bySource,
                    detail,
                    type,
                    quantityDelta);

                return ToMovementDto(bySource);
            }

            if (type == "Sale")
            {
                if (!product.IsActive)
                {
                    throw new BusinessRuleException(
                        "Sản phẩm đã ngừng bán.");
                }
            }
            else
            {
                // Chỉ hoàn tồn nếu dòng món đã được trừ tồn trước đó.
                var sale = await _movementRepository.GetByOrderDetailAsync(
                    orderDetailId,
                    "Sale",
                    cancellationToken);

                if (sale is null)
                {
                    throw new BusinessRuleException(
                        "Dòng món chưa được xuất kho nên không thể hoàn tồn.");
                }

                EnsureSameMovement(
                    sale,
                    detail,
                    "Sale",
                    -quantity);
            }

            var reason = type == "Sale"
                ? $"Xuất kho cho dòng món {orderDetailId}."
                : $"Hoàn kho cho dòng món {orderDetailId}.";

            // Dùng transaction hiện có; bên gọi quyết định commit/rollback.
            return await ChangeStockAsync(
                product,
                type,
                quantityDelta,
                reason,
                employeeId,
                operationKey,
                detail,
                cancellationToken);
        }

        // Cập nhật tồn và ghi lịch sử trong cùng transaction.
        private async Task<InventoryMovementDto> ChangeStockAsync(
            Product product,
            string type,
            int quantityDelta,
            string reason,
            int? employeeId,
            string operationKey,
            OrderDetail? detail,
            CancellationToken cancellationToken)
        {
            var before = product.Stock;

            // Tính bằng long để tránh tràn số int.
            var after = (long)before + quantityDelta;

            if (before < 0 || after < 0 || after > int.MaxValue)
            {
                throw new BusinessRuleException(
                    "Tồn kho sau thao tác không hợp lệ hoặc không đủ để xuất.");
            }

            product.Stock = (int)after;

            var movement = new InventoryTransaction
            {
                ProductId = product.ProductId,
                EmployeeId = employeeId,
                Type = type,
                Quantity = quantityDelta,
                Before = before,
                After = product.Stock,
                Reason = reason,
                ReferenceType = detail is null ? "Manual" : "Order",
                OperationKey = operationKey,
                ReceiptId = null,
                OrderId = detail?.OrderId,
                OrderDetailId = detail?.OrderDetailId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _movementRepository.AddAsync(
                movement,
                cancellationToken);

            // Lưu cả tồn và lịch sử, đồng thời lấy ID do SQL tạo.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ToMovementDto(movement);
        }

        // Kiểm tra dữ liệu nhập/điều chỉnh thủ công.
        private static CreateInventoryMovementRequest ValidateRequest(
            CreateInventoryMovementRequest request)
        {
            if (request is null)
            {
                throw new ValidationException(
                    "Phải cung cấp dữ liệu thay đổi tồn kho.");
            }

            if (request.ProductId <= 0)
            {
                throw new ValidationException(
                    "ID sản phẩm phải lớn hơn 0.");
            }

            // Sale và Return chỉ được gọi qua luồng đơn.
            if (request.Type is not ("Receive" or "Adjust"))
            {
                throw new ValidationException(
                    "Thao tác thủ công chỉ hỗ trợ Receive hoặc Adjust.");
            }

            if (request.QuantityDelta == 0)
            {
                throw new ValidationException(
                    "Số lượng thay đổi phải khác 0.");
            }

            if (request.Type == "Receive" && request.QuantityDelta < 0)
            {
                throw new ValidationException(
                    "Số lượng nhập kho phải lớn hơn 0.");
            }

            var reason = request.Reason?.Trim() ?? string.Empty;

            if (reason.Length is < 1 or > 200)
            {
                throw new ValidationException(
                    "Lý do phải có từ 1 đến 200 ký tự sau khi bỏ khoảng trắng hai đầu.");
            }

            return request with { Reason = reason };
        }

        // Kiểm tra đầu vào của thao tác xuất/hoàn theo đơn.
        private static void ValidateOrderMovement(
            int orderDetailId,
            int productId,
            int quantity,
            int? employeeId,
            string operationKey)
        {
            if (orderDetailId <= 0 || productId <= 0)
            {
                throw new ValidationException(
                    "ID dòng món và sản phẩm phải lớn hơn 0.");
            }

            if (quantity <= 0)
            {
                throw new ValidationException(
                    "Số lượng xuất hoặc hoàn phải lớn hơn 0.");
            }

            // Null dành cho nguồn khách/hệ thống.
            if (employeeId is <= 0)
            {
                throw new ValidationException(
                    "ID nhân viên phải lớn hơn 0 hoặc để null.");
            }

            if (string.IsNullOrWhiteSpace(operationKey)
                || operationKey.Length > 200)
            {
                throw new ValidationException(
                    "Khóa thao tác phải có từ 1 đến 200 ký tự.");
            }

            // Giữ khóa chính xác, không âm thầm sửa khóa của bên gọi.
            if (!string.Equals(
                    operationKey,
                    operationKey.Trim(),
                    StringComparison.Ordinal))
            {
                throw new ValidationException(
                    "Khóa thao tác không được có khoảng trắng ở hai đầu.");
            }
        }

        // Khóa hoặc nguồn đã tồn tại phải khớp nội dung yêu cầu.
        private static void EnsureSameMovement(
            InventoryTransaction movement,
            OrderDetail detail,
            string type,
            int quantityDelta)
        {
            if (movement.ProductId != detail.ProductId
                || movement.OrderId != detail.OrderId
                || movement.OrderDetailId != detail.OrderDetailId
                || movement.Type != type
                || movement.Quantity != quantityDelta)
            {
                throw new BusinessRuleException(
                    "Khóa thao tác hoặc nguồn biến động đã được dùng cho nội dung khác.");
            }
        }

        // Chuyển lịch sử sang DTO trả về.
        private static InventoryMovementDto ToMovementDto(
            InventoryTransaction movement)
        {
            return new InventoryMovementDto(
                movement.InvTransId,
                movement.ProductId,
                movement.Type,
                movement.Quantity,
                movement.After,
                movement.Reason,
                movement.OrderDetailId,
                movement.EmployeeId,
                movement.CreatedAt);
        }

        // Chuyển sản phẩm sang DTO tồn kho.
        private static InventoryItemDto ToItemDto(Product product)
        {
            return new InventoryItemDto(
                product.ProductId,
                product.ProductName,
                product.Stock,
                product.IsActive,
                product.Price,
                product.CategoryId);
        }
    }
}
