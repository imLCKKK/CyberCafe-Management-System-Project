using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Repositories
{
    public sealed class InventoryMovementRepository : IInventoryMovementRepository
    {
        private readonly ApplicationDbContext _context;

        public InventoryMovementRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        //Đọc tối đa 500 biến động mới nhất, chuyển thành DTO
        public async Task<IReadOnlyList<InventoryMovementDto>> GetRecentAsync(CancellationToken cancellationToken = default)
        {
            return await _context.InventoryTransactions
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.InvTransId)
                .Take(500)
                .Select(x => new InventoryMovementDto(
                    x.InvTransId,
                    x.ProductId,
                    x.Type,
                    x.Quantity,
                    x.After,
                    x.Reason,
                    x.OrderDetailId,
                    x.EmployeeId,
                    x.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        //	Tìm biến động theo khóa thao tác để xử lý retry
        public async Task<InventoryTransaction?> GetByOperationKeyAsync(string operationKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(operationKey) || operationKey.Length > 200)
            {
                throw new ArgumentException("Khóa thao tác phải có từ 1 đến 200 ký tự.", nameof(operationKey));
            }

            return await _context.InventoryTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.OperationKey == operationKey,
                    cancellationToken);
        }

        //	Kiểm tra dòng món đã xuất hoặc hoàn tồn chưa
        public async Task<InventoryTransaction?> GetByOrderDetailAsync(int orderDetailId, string type, CancellationToken cancellationToken = default)
        {
            if (orderDetailId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(orderDetailId));
            }

            if (type is not ("Sale" or "Return"))
            {
                throw new ArgumentException("Loại biến động theo dòng món phải là Sale hoặc Return.", nameof(type));
            }

            return await _context.InventoryTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.OrderDetailId == orderDetailId
                        && x.Type == type,
                    cancellationToken);
        }

        //	Đưa lịch sử mới vào DbContext để chờ lưu
        public async Task AddAsync(InventoryTransaction movement, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(movement);

            if (_context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("Phải mở transaction trước khi thêm lịch sử kho.");
            }

            await _context.InventoryTransactions.AddAsync(movement, cancellationToken);
        }
    }
}
