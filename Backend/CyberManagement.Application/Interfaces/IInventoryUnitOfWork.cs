using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces
{
    public interface IInventoryUnitOfWork
    {
        // Mở transaction, chạy operation và commit khi thành công.
        // Có lỗi thì rollback và ném lại lỗi.
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

        // Lưu các thay đổi trong transaction hiện tại.
        // Dùng trước khi ánh xạ bản ghi mới để lấy ID do SQL sinh.
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
