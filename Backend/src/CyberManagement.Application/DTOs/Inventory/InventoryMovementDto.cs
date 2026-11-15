using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Inventory
{
    public sealed record InventoryMovementDto(
        int Id,
        int ProductId,
        string Type,
        int QuantityDelta,
        int StockAfter,
        string Reason,
        int? OrderItemId,
        int? EmployeeId,
        DateTimeOffset RecordedAt);
}
