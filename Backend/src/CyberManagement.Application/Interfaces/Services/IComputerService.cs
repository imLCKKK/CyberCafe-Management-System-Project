using CyberManagement.Application.DTOs.Computers;

namespace CyberManagement.Application.Interfaces.Services;

public interface IComputerService
{
    Task<IReadOnlyList<ComputerDto>> GetComputersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ComputerTypeDto>> GetComputerTypesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ComputerRoomDto>> GetComputerRoomsAsync(CancellationToken cancellationToken);
    Task<ComputerDto> UpdateStatusAsync(
        int computerId, UpdateComputerStatusCommand command, CancellationToken cancellationToken);
    Task<ComputerDto> SaveComputerAsync(int? computerId, SaveComputerCommand command, CancellationToken cancellationToken);
    Task DeleteComputerAsync(int computerId, CancellationToken cancellationToken);
    Task<ComputerTypeDto> SaveComputerTypeAsync(int? typeId, SaveComputerTypeCommand command, CancellationToken cancellationToken);
    Task DeleteComputerTypeAsync(int typeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ComputerMaintenanceDto>> GetMaintenancesAsync(int computerId, CancellationToken cancellationToken);
    Task<ComputerMaintenanceDto> StartMaintenanceAsync(
        int computerId, int employeeId, SaveMaintenanceCommand command, CancellationToken cancellationToken);
    Task<ComputerMaintenanceDto> UpdateMaintenanceAsync(
        int computerId, int maintenanceId, SaveMaintenanceCommand command, CancellationToken cancellationToken);
    Task<ComputerMaintenanceDto> CompleteMaintenanceAsync(int computerId, int maintenanceId, CancellationToken cancellationToken);
}
