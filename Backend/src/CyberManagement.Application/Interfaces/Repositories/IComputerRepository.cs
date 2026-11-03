using CyberManagement.Application.DTOs.Computers;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface IComputerRepository
{
    Task<IReadOnlyList<ComputerDto>> GetComputersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ComputerTypeDto>> GetComputerTypesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ComputerRoomDto>> GetComputerRoomsAsync(CancellationToken cancellationToken);
    Task<ComputerDto> UpdateStatusAsync(
        int computerId, UpdateComputerStatusCommand command, CancellationToken cancellationToken);
}
