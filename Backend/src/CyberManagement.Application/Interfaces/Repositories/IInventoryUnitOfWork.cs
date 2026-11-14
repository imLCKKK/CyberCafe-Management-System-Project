using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInventoryUnitOfWork
    {
        // Cho biết DbContext đang có transaction hay chưa.
        bool HasActiveTransaction { get; }

        // Thực hiện một nhóm thao tác trong transaction.
        // Nếu tham gia transaction có sẵn, không tự commit transaction đó.
        Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default);

        // Ghi các thay đổi đang được EF theo dõi xuống SQL.
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
