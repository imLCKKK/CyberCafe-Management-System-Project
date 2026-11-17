using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Exceptions;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class IdentityRepository(ApplicationDbContext database) : IIdentityRepository
{
    public Task<Employee?> FindEmployeeForAuthenticationAsync(
        string normalizedUsername,
        CancellationToken cancellationToken) =>
        database.Employees
            .Include(employee => employee.Role)
                .ThenInclude(role => role.RolePermissions)
                    .ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(
                employee => employee.Username.ToUpper() == normalizedUsername,
                cancellationToken);

    public Task<bool> EmployeeUsernameExistsAsync(
        string username,
        CancellationToken cancellationToken) =>
        database.Employees.AnyAsync(employee => employee.Username == username, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken) =>
        await database.Roles.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken) =>
        await database.Permissions.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RolePermission>> GetRolePermissionsAsync(
        CancellationToken cancellationToken) =>
        await database.RolePermissions.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesWithPermissionsAsync(
        CancellationToken cancellationToken) =>
        await database.Roles
            .AsNoTracking()
            .Include(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .ToListAsync(cancellationToken);

    public Task<Employee?> GetEmployeeByIdAsync(
        int employeeId,
        CancellationToken cancellationToken) =>
        database.Employees
            .Include(e => e.Role)
            .SingleOrDefaultAsync(e => e.EmployeeId == employeeId, cancellationToken);

    public Task<Employee?> GetEmployeeByCreateKeyAsync(
        string createRequestKey,
        CancellationToken cancellationToken) =>
        database.Employees
            .Include(e => e.Role)
            .SingleOrDefaultAsync(e => e.CreateRequestKey == createRequestKey, cancellationToken);

    public async Task<IReadOnlyList<Employee>> GetAllEmployeesAsync(
        CancellationToken cancellationToken) =>
        await database.Employees
            .Include(e => e.Role)
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);

    public void AddRole(Role role) => database.Roles.Add(role);

    public void AddPermission(Permission permission) => database.Permissions.Add(permission);

    public void AddRolePermission(RolePermission rolePermission) =>
        database.RolePermissions.Add(rolePermission);

    public void RemoveRolePermission(RolePermission rolePermission) =>
        database.RolePermissions.Remove(rolePermission);

    public void AddEmployee(Employee employee) => database.Employees.Add(employee);

    public void RemoveEmployee(Employee employee) => database.Employees.Remove(employee);

    public void UpdateEmployee(Employee employee) => database.Employees.Update(employee);

    public async Task SaveEmployeeWithAuditAsync(
        Employee employee,
        AuditLog audit,
        bool preserveAnotherActiveAdmin,
        CancellationToken cancellationToken)
    {
        var isNewEmployee = database.Entry(employee).State == EntityState.Added;
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (isNewEmployee && employee.CreateRequestKey is not null)
        {
            if (database.Database.IsSqlServer())
            {
                var lockResource = $"EmployeeCreate:{employee.CreateRequestKey}";
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"DECLARE @lockResult int; EXEC @lockResult = sys.sp_getapplock @Resource = {lockResource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @lockResult < 0 THROW 51001, 'Could not acquire employee create idempotency lock.', 1;",
                    cancellationToken);
            }

            if (await database.Employees.AnyAsync(
                    row => row.CreateRequestKey == employee.CreateRequestKey,
                    cancellationToken))
                throw new ConflictException("Idempotency-Key đã được sử dụng.");
        }

        if (preserveAnotherActiveAdmin)
        {
            if (database.Database.IsSqlServer())
            {
                const string lockResource = "EmployeeAdminInvariant";
                await database.Database.ExecuteSqlInterpolatedAsync(
                    $"DECLARE @lockResult int; EXEC @lockResult = sys.sp_getapplock @Resource = {lockResource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @lockResult < 0 THROW 51000, 'Could not acquire employee admin invariant lock.', 1;",
                    cancellationToken);
            }

            var anotherActiveAdminExists = await database.Employees
                .AnyAsync(employeeRow =>
                    employeeRow.EmployeeId != employee.EmployeeId
                    && employeeRow.IsActive
                    && employeeRow.Role.RoleName == "Admin",
                    cancellationToken);
            if (!anotherActiveAdminExists)
                throw new BusinessRuleException("Không thể thay đổi trạng thái hoặc quyền của Admin hoạt động cuối cùng.");
        }

        // A new employee needs its SQL-generated ID before it can be the audit target.
        if (isNewEmployee)
            await database.SaveChangesAsync(cancellationToken);

        audit.EntityId = employee.EmployeeId;
        database.AuditLogs.Add(audit);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("VersionConflict: hồ sơ nhân viên đã thay đổi. Vui lòng tải lại.");
        }
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}
