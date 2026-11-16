using System.Data;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class ComputerRepository(ApplicationDbContext database) : IComputerRepository
{
    public async Task<IReadOnlyList<ComputerDto>> GetComputersAsync(CancellationToken cancellationToken) =>
        await database.Computers.AsNoTracking()
            .OrderBy(computer => computer.RoomId)
            .ThenBy(computer => computer.ComputerName)
            .ThenBy(computer => computer.ComputerId)
            .Select(computer => new ComputerDto(
                computer.ComputerId, computer.ComputerName, computer.RoomId, computer.ComputerTypeId,
                computer.Status,
                computer.GamingSessions.Any(session => session.Status == "Active" || session.EndTime == null),
                computer.Maintenances.Any(maintenance => maintenance.EndTime == null)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ComputerTypeDto>> GetComputerTypesAsync(CancellationToken cancellationToken) =>
        await database.ComputerTypes.AsNoTracking()
            .OrderBy(type => type.ComputerTypeId)
            .Select(type => new ComputerTypeDto(
                type.ComputerTypeId, type.TypeName, type.CPU, type.RAM, type.GPU,
                type.PricePerHour, type.Description))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ComputerRoomDto>> GetComputerRoomsAsync(CancellationToken cancellationToken) =>
        await database.ComputerRooms.AsNoTracking()
            .OrderBy(room => room.RoomId)
            .Select(room => new ComputerRoomDto(room.RoomId, room.RoomName, room.Floor, room.Description))
            .ToListAsync(cancellationToken);

    public async Task<ComputerStatusUpdateResult> UpdateStatusAsync(
        int computerId, UpdateComputerStatusCommand command, CancellationToken cancellationToken)
    {
        // InMemory is only for functional checks; it cannot verify SQL locking or rollback.
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        // Serialize writers before reading state; serializable predicate reads also protect open-session ranges.
        var computers = database.Database.IsSqlServer()
            ? database.Computers.FromSqlInterpolated(
                $"SELECT * FROM [Computers] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerId] = {computerId}")
            : database.Computers.Where(computer => computer.ComputerId == computerId);
        var computer = await computers.SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy máy tính.");

        var hasOpenSession = await database.GamingSessions.AnyAsync(
            session => session.ComputerId == computerId && (session.Status == "Active" || session.EndTime == null),
            cancellationToken);
        var hasOpenMaintenance = await database.ComputerMaintenances.AnyAsync(
            maintenance => maintenance.ComputerId == computerId && maintenance.EndTime == null,
            cancellationToken);

        if (computer.Status is not ("Available" or "Maintenance"))
            throw new BusinessRuleException("Máy đang được sử dụng hoặc có trạng thái cần đối soát.");
        if (hasOpenSession)
            throw new BusinessRuleException("Không thể thay đổi máy khi còn phiên chưa kết thúc.");
        if (command.Status == "Available" && hasOpenMaintenance)
            throw new BusinessRuleException("Cần kết thúc hồ sơ bảo trì trước khi đưa máy về trạng thái trống.");

        // A retry is a no-op only after all safety checks, including inconsistent session data.
        var changed = computer.Status != command.Status;
        if (changed)
        {
            if (computer.Status != command.ExpectedStatus)
                throw new BusinessRuleException("Trạng thái máy đã thay đổi. Vui lòng tải lại dữ liệu.");

            computer.Status = command.Status;
            await database.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return new ComputerStatusUpdateResult(
            new ComputerDto(computer.ComputerId, computer.ComputerName, computer.RoomId,
                computer.ComputerTypeId, computer.Status, hasOpenSession, hasOpenMaintenance), changed);
    }

    public async Task<ComputerDto> SaveComputerAsync(
        int? computerId, SaveComputerCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        // Use the same parent lock as type deletion so a new reference cannot race its removal.
        await LockTypeAsync(command.ComputerTypeId, cancellationToken);
        // Lock the target room to serialize duplicate-name checks, including creates with different types.
        var rooms = database.Database.IsSqlServer()
            ? database.ComputerRooms.FromSqlInterpolated(
                $"SELECT * FROM [ComputerRooms] WITH (UPDLOCK, HOLDLOCK) WHERE [RoomId] = {command.RoomId}")
            : database.ComputerRooms.Where(row => row.RoomId == command.RoomId);
        if (!await rooms.AnyAsync(cancellationToken))
            throw new EntityNotFoundException("Không tìm thấy phòng máy.");

        var computer = computerId.HasValue
            ? await LockComputerAsync(computerId.Value, cancellationToken)
            : new Computer { Status = "Available" };
        if (computerId.HasValue) await EnsureEditableAsync(computer, cancellationToken);
        var name = command.ComputerName.Trim();
        if (await database.Computers.AnyAsync(row => row.RoomId == command.RoomId && row.ComputerName == name
            && (!computerId.HasValue || row.ComputerId != computerId.Value), cancellationToken))
            throw new BusinessRuleException("Tên máy đã tồn tại trong phòng này.");

        computer.ComputerName = name;
        computer.RoomId = command.RoomId;
        computer.ComputerTypeId = command.ComputerTypeId;
        if (!computerId.HasValue) database.Computers.Add(computer);
        await SaveAndCommitAsync(transaction, cancellationToken);
        return new ComputerDto(computer.ComputerId, computer.ComputerName, computer.RoomId,
            computer.ComputerTypeId, computer.Status, false, false);
    }

    public async Task DeleteComputerAsync(int computerId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var computer = await LockComputerAsync(computerId, cancellationToken);
        await EnsureEditableAsync(computer, cancellationToken);
        if (await database.GamingSessions.AnyAsync(row => row.ComputerId == computerId, cancellationToken)
            || await database.ComputerMaintenances.AnyAsync(row => row.ComputerId == computerId, cancellationToken))
            throw new BusinessRuleException("Không thể xóa máy đã có lịch sử phiên hoặc bảo trì.");
        database.Computers.Remove(computer);
        await SaveAndCommitAsync(transaction, cancellationToken);
    }

    public async Task<ComputerTypeDto> SaveComputerTypeAsync(
        int? typeId, SaveComputerTypeCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var type = typeId.HasValue ? await LockTypeAsync(typeId.Value, cancellationToken) : new ComputerType();
        type.TypeName = command.TypeName.Trim();
        type.PricePerHour = command.PricePerHour;
        type.CPU = command.CPU?.Trim();
        type.RAM = command.RAM?.Trim();
        type.GPU = command.GPU?.Trim();
        type.Description = command.Description?.Trim();
        if (!typeId.HasValue) database.ComputerTypes.Add(type);
        await SaveAndCommitAsync(transaction, cancellationToken);
        return new ComputerTypeDto(type.ComputerTypeId, type.TypeName, type.CPU, type.RAM, type.GPU,
            type.PricePerHour, type.Description);
    }

    public async Task DeleteComputerTypeAsync(int typeId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var type = await LockTypeAsync(typeId, cancellationToken);
        if (await database.Computers.AnyAsync(row => row.ComputerTypeId == typeId, cancellationToken))
            throw new BusinessRuleException("Không thể xóa loại máy đang được tham chiếu.");
        database.ComputerTypes.Remove(type);
        await SaveAndCommitAsync(transaction, cancellationToken);
    }

    public async Task<IReadOnlyList<ComputerMaintenanceDto>> GetMaintenancesAsync(int computerId, CancellationToken cancellationToken)
    {
        if (!await database.Computers.AnyAsync(row => row.ComputerId == computerId, cancellationToken))
            throw new EntityNotFoundException("Không tìm thấy máy tính.");
        var records = await database.ComputerMaintenances.AsNoTracking().Where(row => row.ComputerId == computerId)
            .OrderByDescending(row => row.StartTime).ThenByDescending(row => row.MaintenanceId).ToListAsync(cancellationToken);
        return records.Select(ToMaintenanceDto).ToArray();
    }

    public async Task<MaintenanceUpdateResult> StartMaintenanceAsync(
        int computerId, int employeeId, SaveMaintenanceCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var computer = await LockComputerAsync(computerId, cancellationToken);
        await EnsureEditableAsync(computer, cancellationToken);
        if (!await database.Employees.AnyAsync(row => row.EmployeeId == employeeId && row.IsActive, cancellationToken))
            throw new UnauthorizedAccessException("Nhân viên không còn hoạt động.");
        var maintenance = new ComputerMaintenance
        {
            ComputerId = computerId, EmployeeId = employeeId, Description = command.Description?.Trim(),
            StartTime = DateTime.UtcNow, Status = "InProgress"
        };
        computer.Status = "Maintenance";
        database.ComputerMaintenances.Add(maintenance);
        await SaveAndCommitAsync(transaction, cancellationToken);
        return new MaintenanceUpdateResult(ToMaintenanceDto(maintenance), true);
    }

    public async Task<MaintenanceUpdateResult> UpdateMaintenanceAsync(
        int computerId, int maintenanceId, SaveMaintenanceCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var computer = await LockComputerAsync(computerId, cancellationToken);
        var maintenance = await FindMaintenanceAsync(computerId, maintenanceId, cancellationToken);
        await EnsureMaintenanceOpenAsync(computer, maintenance, cancellationToken);
        var description = command.Description?.Trim();
        var changed = maintenance.Description != description;
        maintenance.Description = description;
        await SaveAndCommitAsync(transaction, cancellationToken);
        return new MaintenanceUpdateResult(ToMaintenanceDto(maintenance), changed);
    }

    public async Task<MaintenanceUpdateResult> CompleteMaintenanceAsync(
        int computerId, int maintenanceId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var computer = await LockComputerAsync(computerId, cancellationToken);
        var maintenance = await FindMaintenanceAsync(computerId, maintenanceId, cancellationToken);
        // A retry only reads its completed record and must not free a machine used by a later operation.
        if (maintenance.EndTime.HasValue)
        {
            if (maintenance.Status != "Completed")
                throw new BusinessRuleException("Hồ sơ bảo trì cần đối soát.");
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new MaintenanceUpdateResult(ToMaintenanceDto(maintenance), false);
        }
        await EnsureMaintenanceOpenAsync(computer, maintenance, cancellationToken);
        var now = DateTime.UtcNow;
        if (maintenance.StartTime > now)
            throw new BusinessRuleException("Thời điểm bắt đầu bảo trì cần đối soát.");
        maintenance.EndTime = now;
        maintenance.Status = "Completed";
        computer.Status = "Available";
        await SaveAndCommitAsync(transaction, cancellationToken);
        return new MaintenanceUpdateResult(ToMaintenanceDto(maintenance), true);
    }

    private async Task EnsureEditableAsync(Computer computer, CancellationToken cancellationToken)
    {
        if (computer.Status is not ("Available" or "Maintenance")
            || await HasOpenSessionAsync(computer.ComputerId, cancellationToken)
            || await database.ComputerMaintenances.AnyAsync(row => row.ComputerId == computer.ComputerId && row.EndTime == null,
                cancellationToken))
            throw new BusinessRuleException("Cần kết thúc phiên và hồ sơ bảo trì, kiểm tra trạng thái máy trước khi thao tác.");
    }

    private async Task EnsureMaintenanceOpenAsync(
        Computer computer, ComputerMaintenance maintenance, CancellationToken cancellationToken)
    {
        if (computer.Status != "Maintenance" || maintenance.EndTime.HasValue || maintenance.Status != "InProgress"
            || await HasOpenSessionAsync(computer.ComputerId, cancellationToken)
            || await database.ComputerMaintenances.AnyAsync(row => row.ComputerId == computer.ComputerId
                && row.EndTime == null && row.MaintenanceId != maintenance.MaintenanceId, cancellationToken))
            throw new BusinessRuleException("Máy hoặc hồ sơ bảo trì không ở trạng thái hợp lệ; cần đối soát trước khi thao tác.");
    }

    private Task<bool> HasOpenSessionAsync(int computerId, CancellationToken cancellationToken) =>
        database.GamingSessions.AnyAsync(row => row.ComputerId == computerId && (row.Status == "Active" || row.EndTime == null),
            cancellationToken);

    private async Task<Computer> LockComputerAsync(int computerId, CancellationToken cancellationToken)
    {
        var query = database.Database.IsSqlServer()
            ? database.Computers.FromSqlInterpolated(
                $"SELECT * FROM [Computers] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerId] = {computerId}")
            : database.Computers.Where(row => row.ComputerId == computerId);
        return await query.SingleOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("Không tìm thấy máy tính.");
    }

    private async Task<ComputerType> LockTypeAsync(int typeId, CancellationToken cancellationToken)
    {
        var query = database.Database.IsSqlServer()
            ? database.ComputerTypes.FromSqlInterpolated(
                $"SELECT * FROM [ComputerTypes] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerTypeId] = {typeId}")
            : database.ComputerTypes.Where(row => row.ComputerTypeId == typeId);
        return await query.SingleOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("Không tìm thấy loại máy.");
    }

    private async Task<ComputerMaintenance> FindMaintenanceAsync(int computerId, int maintenanceId, CancellationToken cancellationToken) =>
        await database.ComputerMaintenances.SingleOrDefaultAsync(
            row => row.ComputerId == computerId && row.MaintenanceId == maintenanceId, cancellationToken)
        ?? throw new EntityNotFoundException("Không tìm thấy hồ sơ bảo trì của máy.");

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;

    private async Task SaveAndCommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 or 547 })
        {
            throw new BusinessRuleException("Dữ liệu bị trùng hoặc đã được tham chiếu. Vui lòng tải lại.");
        }
    }

    private static ComputerMaintenanceDto ToMaintenanceDto(ComputerMaintenance row) =>
        new(row.MaintenanceId, row.ComputerId, row.EmployeeId, row.Description,
            DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc),
            row.EndTime.HasValue ? DateTime.SpecifyKind(row.EndTime.Value, DateTimeKind.Utc) : null, row.Status);
}
