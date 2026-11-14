using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Inventory
{
    public sealed record CreateInventoryMovementRequest(
        int ProductId,
        string Type, //Receive or Adjust.
        int QuantityDelta, //increase or decrease in quantity.
        string Reason
    );
}
