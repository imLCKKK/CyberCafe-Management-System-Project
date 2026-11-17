using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.DTOs.Payments;

public sealed record ZaloPaySettlementResult(ZaloPayIntent Intent, bool Applied);
