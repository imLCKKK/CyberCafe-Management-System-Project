using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Application.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Repositories
{
    public sealed class InventoryUnitOfWork : IInventoryUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public InventoryUnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // Kiểm tra DbContext đang có transaction hay chưa.
        public bool HasActiveTransaction =>
            _context.Database.CurrentTransaction is not null;

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();

            // Dùng transaction của bên gọi nếu đã có.
            if (HasActiveTransaction)
            {
                return await operation();
            }

            // Chưa có transaction thì tự mở một transaction mới.
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Chạy các thao tác do service truyền vào.
                var result = await operation();

                // Xác nhận toàn bộ thay đổi của transaction.
                await transaction.CommitAsync(cancellationToken);

                return result;
            }
            catch
            {
                try
                {
                    // Vẫn hoàn tác dù request đã bị hủy.
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                finally
                {
                    // Bỏ dữ liệu đang theo dõi sau khi hoàn tác.
                    _context.ChangeTracker.Clear();
                }

                throw;
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!HasActiveTransaction)
                throw new InvalidOperationException("Phải mở transaction trước khi lưu thay đổi kho.");

            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql
                    && (sql.Number is 2601 or 2627)
                    && (
                        sql.Message.Contains(
                            "IX_InventoryTransactions_OperationKey",
                            StringComparison.Ordinal)
                        || sql.Message.Contains(
                            "IX_InventoryTransactions_OrderDetailId_Type",
                            StringComparison.Ordinal)))
            {
                throw new BusinessRuleException(
                    "Thao tác kho bị trùng khóa hoặc dòng món đã được xử lý. "
                    + "Hãy gửi lại toàn bộ thao tác với khóa ban đầu.");
            }
        }
    }
}
