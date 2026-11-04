using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface IIdentityRepository
{
    Task<Employee?> FindEmployeeForAuthenticationAsync(
        string normalizedUsername,
        CancellationToken cancellationToken);

    Task<bool> EmployeeUsernameExistsAsync(string username, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RolePermission>> GetRolePermissionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetRolesWithPermissionsAsync(CancellationToken cancellationToken);

    void AddRole(Role role);

    void AddPermission(Permission permission);

    void AddRolePermission(RolePermission rolePermission);

    void RemoveRolePermission(RolePermission rolePermission);

    void AddEmployee(Employee employee);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
