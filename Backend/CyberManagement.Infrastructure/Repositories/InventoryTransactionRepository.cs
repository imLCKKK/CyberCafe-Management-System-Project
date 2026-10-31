using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public class InventoryTransactionRepository : IInventoryTransactionRepository
{
    private readonly ApplicationDbContext _context;

    public InventoryTransactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(InventoryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await _context.InventoryTransactions.AddAsync(transaction);
    }

    public async Task<IEnumerable<InventoryTransaction>> GetByProductIdAsync(long productId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        return await _context.InventoryTransactions
            .AsNoTracking()
            .Where(movement => movement.ProductId == productId)
            .OrderByDescending(movement => movement.CreatedAt)
            .ThenByDescending(movement => movement.InvTransId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<InventoryMovementDto>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InventoryTransactions
            .AsNoTracking()
            .OrderByDescending(movement => movement.CreatedAt) // bản ghi mới nhất lên đầu
            .ThenByDescending(movement => movement.InvTransId) // ổn định thứ tự nếu trùng thời điểm
            .Take(500) // giới hạn số lượng bản ghi trả về
            .Select(movement => new InventoryMovementDto
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
            })
            .ToListAsync(cancellationToken);
    }
}