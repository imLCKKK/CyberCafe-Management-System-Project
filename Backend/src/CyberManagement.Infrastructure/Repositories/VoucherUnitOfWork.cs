using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Repositories
{
    // Quản lý transaction và lưu các thay đổi voucher cùng nhau.
    public sealed class VoucherUnitOfWork : IVoucherUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public VoucherUnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // Cho biết DbContext đang có transaction hay không.
        public bool HasActiveTransaction =>
            _context.Database.CurrentTransaction is not null;

        // Chạy công việc trong transaction có sẵn hoặc tự mở transaction mới.
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();

            // Dùng transaction của bên đặt món nếu đã có.
            if (HasActiveTransaction)
                return await operation();

            // Tự mở transaction nếu bên gọi chưa mở.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await operation();

                // Xác nhận toàn bộ thay đổi khi công việc thành công.
                await transaction.CommitAsync(cancellationToken);

                return result;
            }
            catch
            {
                try
                {
                    // Có lỗi thì hoàn tác, kể cả khi request đã bị hủy.
                    await transaction.RollbackAsync(
                        CancellationToken.None);
                }
                finally
                {
                    // Bỏ dữ liệu EF đang giữ sau khi hoàn tác.
                    _context.ChangeTracker.Clear();
                }

                throw;
            }
        }

        // Ghi thay đổi xuống database trong transaction; chưa commit.
        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (!HasActiveTransaction)
                throw new InvalidOperationException("Phải mở transaction trước khi lưu thay đổi voucher.");

            try
            {
                _context.ChangeTracker.DetectChanges();

                return await _context.SaveChangesAsync(
                    cancellationToken);
            }
            // Đổi lỗi trùng mã hoặc trùng lượt của đơn thành lỗi nghiệp vụ.
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sql
                    && (sql.Number is 2601 or 2627)
                    && (
                        sql.Message.Contains(
                            "IX_Promotions_Code",
                            StringComparison.Ordinal)
                        || sql.Message.Contains(
                            "IX_PromotionUsages_OrderId",
                            StringComparison.Ordinal)))
            {
                throw new BusinessRuleException(
                    "Mã voucher bị trùng hoặc đơn đã có lượt sử dụng voucher.");
            }
        }
    }
}
