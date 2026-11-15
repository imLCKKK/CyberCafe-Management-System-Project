using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

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

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}
