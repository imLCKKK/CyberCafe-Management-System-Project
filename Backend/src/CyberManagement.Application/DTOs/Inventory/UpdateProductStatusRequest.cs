using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Inventory
{
    public sealed record UpdateProductStatusRequest(bool? IsActive);
}
