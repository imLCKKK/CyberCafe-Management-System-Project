using System.Data;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

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

    public async Task<ComputerDto> UpdateStatusAsync(
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
        if (computer.Status != command.Status)
        {
            if (computer.Status != command.ExpectedStatus)
                throw new BusinessRuleException("Trạng thái máy đã thay đổi. Vui lòng tải lại dữ liệu.");

            computer.Status = command.Status;
            await database.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return new ComputerDto(computer.ComputerId, computer.ComputerName, computer.RoomId,
            computer.ComputerTypeId, computer.Status, hasOpenSession, hasOpenMaintenance);
    }
}
