using CyberManagement.Application.DTOs.Inventory;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface IInventoryQueries
{
    Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(
        CancellationToken cancellationToken = default);
}
