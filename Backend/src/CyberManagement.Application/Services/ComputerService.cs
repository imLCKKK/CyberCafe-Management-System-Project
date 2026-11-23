using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class ComputerService(IComputerRepository repository, IRealtimePublisher publisher) : IComputerService
{
    public Task<IReadOnlyList<ComputerDto>> GetComputersAsync(CancellationToken cancellationToken) =>
        repository.GetComputersAsync(cancellationToken);

    public Task<IReadOnlyList<ComputerTypeDto>> GetComputerTypesAsync(CancellationToken cancellationToken) =>
        repository.GetComputerTypesAsync(cancellationToken);

    public Task<IReadOnlyList<ComputerRoomDto>> GetComputerRoomsAsync(CancellationToken cancellationToken) =>
        repository.GetComputerRoomsAsync(cancellationToken);

    public async Task<ComputerDto> UpdateStatusAsync(
        int computerId, UpdateComputerStatusCommand command, CancellationToken cancellationToken)
    {
        if (computerId <= 0)
            throw new ValidationException("ID máy phải là số nguyên dương.");
        if (command.Status is not ("Available" or "Maintenance")
            || command.ExpectedStatus is not ("Available" or "Maintenance"))
            throw new ValidationException("Trạng thái và trạng thái dự kiến phải là Available hoặc Maintenance.");

        var result = await repository.UpdateStatusAsync(computerId, command, cancellationToken);
        if (result.Changed)
        {
            // Repository returns only after commit; request cancellation must not suppress a committed change.
            await publisher.PublishToPermissionAsync("computer.view",
                new RealtimeEvent(Guid.NewGuid(), RealtimeEvent.ComputerChanged,
                    result.Computer.ComputerId, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        return result.Computer;
    }

    public async Task<ComputerDto> SaveComputerAsync(
        int? computerId, SaveComputerCommand command, CancellationToken cancellationToken)
    {
        if (computerId.HasValue) ValidateId(computerId.Value);
        Validate(command);
        var computer = await repository.SaveComputerAsync(computerId, command, cancellationToken);
        await PublishAsync("computer.view", RealtimeEvent.ComputerChanged, computer.ComputerId);
        return computer;
    }

    public async Task DeleteComputerAsync(int computerId, CancellationToken cancellationToken)
    {
        ValidateId(computerId);
        await repository.DeleteComputerAsync(computerId, cancellationToken);
        await PublishAsync("computer.view", RealtimeEvent.ComputerChanged, computerId);
    }

    public async Task<ComputerTypeDto> SaveComputerTypeAsync(
        int? typeId, SaveComputerTypeCommand command, CancellationToken cancellationToken)
    {
        if (typeId.HasValue) ValidateId(typeId.Value);
        Validate(command);
        if (decimal.Truncate(command.PricePerHour) != command.PricePerHour)
            throw new ValidationException("Giá giờ phải là số nguyên đồng dương.");
        var type = await repository.SaveComputerTypeAsync(typeId, command, cancellationToken);
        await PublishAsync("computer.view", RealtimeEvent.ComputerTypesChanged, type.ComputerTypeId);
        return type;
    }

    public async Task DeleteComputerTypeAsync(int typeId, CancellationToken cancellationToken)
    {
        ValidateId(typeId);
        await repository.DeleteComputerTypeAsync(typeId, cancellationToken);
        await PublishAsync("computer.view", RealtimeEvent.ComputerTypesChanged, typeId);
    }

    public Task<IReadOnlyList<ComputerMaintenanceDto>> GetMaintenancesAsync(int computerId, CancellationToken cancellationToken)
    {
        ValidateId(computerId);
        return repository.GetMaintenancesAsync(computerId, cancellationToken);
    }

    public async Task<ComputerMaintenanceDto> StartMaintenanceAsync(
        int computerId, int employeeId, SaveMaintenanceCommand command, CancellationToken cancellationToken)
    {
        ValidateId(computerId);
        ValidateId(employeeId);
        Validate(command);
        var result = await repository.StartMaintenanceAsync(computerId, employeeId, command, cancellationToken);
        await PublishMaintenanceAsync(result);
        return result.Maintenance;
    }

    public async Task<ComputerMaintenanceDto> UpdateMaintenanceAsync(
        int computerId, int maintenanceId, SaveMaintenanceCommand command, CancellationToken cancellationToken)
    {
        ValidateId(computerId);
        ValidateId(maintenanceId);
        Validate(command);
        var result = await repository.UpdateMaintenanceAsync(computerId, maintenanceId, command, cancellationToken);
        if (result.Changed)
            await PublishAsync("maintenance.view", RealtimeEvent.MaintenanceChanged, computerId);
        return result.Maintenance;
    }

    public async Task<ComputerMaintenanceDto> CompleteMaintenanceAsync(
        int computerId, int maintenanceId, CancellationToken cancellationToken)
    {
        ValidateId(computerId);
        ValidateId(maintenanceId);
        var result = await repository.CompleteMaintenanceAsync(computerId, maintenanceId, cancellationToken);
        if (result.Changed) await PublishMaintenanceAsync(result);
        return result.Maintenance;
    }

    private async Task PublishMaintenanceAsync(MaintenanceUpdateResult result)
    {
        await PublishAsync("computer.view", RealtimeEvent.ComputerChanged, result.Maintenance.ComputerId);
        await PublishAsync("maintenance.view", RealtimeEvent.MaintenanceChanged, result.Maintenance.ComputerId);
    }

    private Task PublishAsync(string permission, string type, int id) =>
        publisher.PublishToPermissionAsync(permission,
            new RealtimeEvent(Guid.NewGuid(), type, id, DateTimeOffset.UtcNow), CancellationToken.None);

    private static void Validate(object command) =>
        Validator.ValidateObject(command, new ValidationContext(command), validateAllProperties: true);

    private static void ValidateId(int id)
    {
        if (id <= 0) throw new ValidationException("ID phải là số nguyên dương.");
    }
}
