using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    // Quản lý transaction và lưu thay đổi voucher.
    public interface IVoucherUnitOfWork
    {
        // Cho biết DbContext đang có transaction hay chưa.
        bool HasActiveTransaction { get; }

        // Tự mở transaction nếu chưa có.
        // Nếu dùng transaction của caller thì không tự commit nó.
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

        // Lưu thay đổi trong transaction hiện tại.
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
