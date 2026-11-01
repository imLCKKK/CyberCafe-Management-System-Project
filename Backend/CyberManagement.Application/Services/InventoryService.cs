using CyberManagement.Application.DTOs;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _movementRepository;
    private readonly IInventoryUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public InventoryService(IInventoryRepository inventoryRepository, ICurrentUser currentUser, IInventoryTransactionRepository movementRepository, IInventoryUnitOfWork unitOfWork)
    {
        _inventoryRepository = inventoryRepository;
        _currentUser = currentUser;
        _movementRepository = movementRepository;
        _unitOfWork = unitOfWork;
    }

    private void RequireStaff()
    {
        if(_currentUser.AccountId <= 0 || !_currentUser.IsActive)
        {
            throw new UnauthorizedAccessException(
                "Tài khoản không hợp lệ hoặc đã bị khóa.");
        }
        if (_currentUser.Role is not ("Admin" or "Staff"))
        {
            throw new UnauthorizedAccessException(
                "Chỉ Admin hoặc Staff được thực hiện thao tác này.");
        }
    }

    // Kiểm tra cả khi service được gọi trực tiếp, không qua controller.
    private static void ValidateRequest(object request)
    {
        var errors = new List<ValidationResult>();

        if(!Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true))
        {
            throw new ValidationException(string.Join("; ", errors.Select(error => error.ErrorMessage)));
        }
    }



    public async Task<InventoryMovementDto> CreateMovementAsync(CreateInventoryMovementRequest request, CancellationToken cancellationToken = default)
    {
        RequireStaff();
        ArgumentNullException.ThrowIfNull(request);

        // Kiểm tra lý do sau khi bỏ khoảng trắng đầu/cuối.
        var normalizedRequest = new CreateInventoryMovementRequest
        {
            ProductId = request.ProductId,
            Type = request.Type,
            QuantityDelta = request.QuantityDelta,
            Reason = request.Reason?.Trim() ?? string.Empty
        };

        ValidateRequest(normalizedRequest);

        return await _unitOfWork.ExecuteAsync(async () =>
        {
            var product = await _inventoryRepository.GetProductForUpdateAsync(normalizedRequest.ProductId, cancellationToken);
            if(product is null)
            {
                throw new EntityNotFoundException($"Không tìm thấy sản phẩm với ID {normalizedRequest.ProductId}.");
            }

            var before = product.Stock;

            var after= (long)before + normalizedRequest.QuantityDelta;

            if(after < 0|| after > int.MaxValue)
            {
                throw new BusinessRuleException($"Số lượng tồn kho sau khi cập nhật ({after}) không hợp lệ.");
            }

            product.Stock = (int)after;

            var movement = new InventoryTransaction
            {
                ProductId = product.ProductId,
                Type = normalizedRequest.Type,
                Quantity = normalizedRequest.QuantityDelta,
                Before = before,
                After = product.Stock,
                Reason = normalizedRequest.Reason,
                OrderDetailId = null,
                EmployeeId = _currentUser.EmployeeId,
                RecordedByAccountId = _currentUser.AccountId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _movementRepository.AddAsync(movement);

            // Lưu tồn và lịch sử cùng transaction; lấy ID do SQL sinh.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new InventoryMovementDto
            {
                Id = movement.InvTransId,
                ProductId = movement.ProductId,
                Type = movement.Type,
                QuantityDelta = movement.Quantity,
                StockAfter = movement.After,
                Reason = movement.Reason,
                OrderItemId = movement.OrderDetailId,
                RecordedByAccountId = movement.RecordedByAccountId,
                RecordedAt = movement.CreatedAt
            };
        }, cancellationToken);

    }

    // Lấy danh sách các sản phẩm tồn kho
    public async Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        RequireStaff();

        return await _inventoryRepository.GetInventoryAsync(cancellationToken);
    }

    // Lấy danh sách các giao dịch tồn kho gần đây
    public async Task<IReadOnlyList<InventoryMovementDto>> GetMovementsAsync(CancellationToken cancellationToken = default)
    {
        RequireStaff();
        return await _movementRepository.GetRecentAsync(cancellationToken);
    }

    public async Task<InventoryItemDto> UpdateProductStatusAsync(long productId, UpdateProductStatusRequest request, CancellationToken cancellationToken = default)
    {
        RequireStaff();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        return await _unitOfWork.ExecuteAsync(async () =>
        {
            var product = await _inventoryRepository.GetProductForUpdateAsync(productId, cancellationToken);
            
            if(product is null)
            {
                throw new EntityNotFoundException($"Không tìm thấy sản phẩm với ID {productId}.");
            }

            // Chỉ đổi trạng thái bán, không thay đổi tồn hoặc giá.
            product.IsActive = request.IsActive!.Value;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new InventoryItemDto
            {
                Id = product.ProductId,
                Name = product.ProductName,
                StockQuantity = product.Stock,
                IsActive = product.IsActive,
                CurrentPrice = product.Price,
                CategoryId = product.CategoryId
            };

        }, cancellationToken);
    }
}