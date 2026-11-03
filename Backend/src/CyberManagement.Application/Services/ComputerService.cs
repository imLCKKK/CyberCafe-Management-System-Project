using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class ComputerService(IComputerRepository repository) : IComputerService
{
    public Task<IReadOnlyList<ComputerDto>> GetComputersAsync(CancellationToken cancellationToken) =>
        repository.GetComputersAsync(cancellationToken);

    public Task<IReadOnlyList<ComputerTypeDto>> GetComputerTypesAsync(CancellationToken cancellationToken) =>
        repository.GetComputerTypesAsync(cancellationToken);

    public Task<IReadOnlyList<ComputerRoomDto>> GetComputerRoomsAsync(CancellationToken cancellationToken) =>
        repository.GetComputerRoomsAsync(cancellationToken);

    public Task<ComputerDto> UpdateStatusAsync(
        int computerId, UpdateComputerStatusCommand command, CancellationToken cancellationToken)
    {
        if (computerId <= 0)
            throw new ValidationException("ID máy phải là số nguyên dương.");
        if (command.Status is not ("Available" or "Maintenance")
            || command.ExpectedStatus is not ("Available" or "Maintenance"))
            throw new ValidationException("Trạng thái và trạng thái dự kiến phải là Available hoặc Maintenance.");

        return repository.UpdateStatusAsync(computerId, command, cancellationToken);
    }
}
