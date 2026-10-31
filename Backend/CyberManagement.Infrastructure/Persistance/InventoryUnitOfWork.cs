using CyberManagement.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Persistance
{
    public class InventoryUnitOfWork : IInventoryUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        public InventoryUnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (_context.Database.CurrentTransaction is not null)
            {
                throw new InvalidOperationException("Đã có transaction đang hoạt động.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken); // CommitAsync xác nhận các thay đổi trong transaction, thành công thì trả về result cho service
                return result;
            }
            catch
            {
                try
                {
                    // Vẫn hoàn tác kể cả khi request đã bị hủy.
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                finally
                {
                    _context.ChangeTracker.Clear();
                }
                throw;
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("Phải mở transaction trước khi lưu thay đổi kho.");
            }

            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
