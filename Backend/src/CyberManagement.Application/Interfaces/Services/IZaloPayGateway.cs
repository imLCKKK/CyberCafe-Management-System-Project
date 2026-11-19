using CyberManagement.Application.DTOs.Payments;

namespace CyberManagement.Application.Interfaces.Services;

public interface IZaloPayGateway
{
    bool Enabled { get; }
    int AppId { get; }
    Task<GatewayCreateResult> CreateAsync(GatewayCreateRequest intent, CancellationToken cancellationToken);
    Task<GatewayQueryResult> QueryAsync(int appId, string merchantTransactionId, CancellationToken cancellationToken);
    GatewayCallback? ReadCallback(string data, string mac, int type);
}
