namespace CyberManagement.Application.Interfaces.Services;

// Internal server boundary: reads the durable receipt itself, never accepts browser payment claims.
public interface IOrderZaloPaySettlement
{
    Task ApplyAsync(int orderId, CancellationToken cancellationToken);
}
