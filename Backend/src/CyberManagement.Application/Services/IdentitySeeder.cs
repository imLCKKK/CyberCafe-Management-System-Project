using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

public sealed class IdentitySeeder(
    IIdentityRepository repository,
    IEmployeePasswordHasher passwordHasher) : IIdentitySeeder
{
    private static readonly IReadOnlyDictionary<string, string> SeedUsernames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Admin"] = "admin",
            ["Manager"] = "manager01",
            ["Cashier"] = "cashier01",
            ["Waiter"] = "waiter01",
            ["Technician"] = "tech01"
        };

    public async Task SeedAsync(IdentitySeedOptions options, CancellationToken cancellationToken)
    {
        var suppliedPasswordCount = SeedUsernames.Keys.Count(role =>
            !string.IsNullOrWhiteSpace(options.AccountPasswords.GetValueOrDefault(role)));
        if (suppliedPasswordCount != 0 && suppliedPasswordCount != SeedUsernames.Count)
        {
            throw new InvalidOperationException(
                "Configure a password for every role in SeedAccounts, or configure none.");
        }

        if (suppliedPasswordCount > 0 && !options.IsDevelopment)
            throw new InvalidOperationException("SeedAccounts may only be used in Development.");

        var roles = (await repository.GetRolesAsync(cancellationToken))
            .ToDictionary(role => role.RoleName, StringComparer.Ordinal);
        foreach (var roleSeed in RolePermissionCatalog.Roles)
        {
            if (!roles.TryGetValue(roleSeed.Name, out var role))
            {
                role = new Role { RoleName = roleSeed.Name, Description = roleSeed.Description };
                repository.AddRole(role);
                roles.Add(roleSeed.Name, role);
            }
            else
            {
                role.Description = roleSeed.Description;
            }
        }

        var permissions = (await repository.GetPermissionsAsync(cancellationToken))
            .ToDictionary(permission => permission.PermissionName, StringComparer.Ordinal);
        foreach (var (name, description) in RolePermissionCatalog.PermissionDescriptions)
        {
            if (!permissions.TryGetValue(name, out var permission))
            {
                permission = new Permission { PermissionName = name, Description = description };
                repository.AddPermission(permission);
                permissions.Add(name, permission);
            }
            else
            {
                permission.Description = description;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);

        var existingAssignments = await repository.GetRolePermissionsAsync(cancellationToken);
        var seededRoleIds = RolePermissionCatalog.Roles
            .Select(roleSeed => roles[roleSeed.Name].RoleId)
            .ToHashSet();
        var desiredAssignments = RolePermissionCatalog.Roles
            .SelectMany(roleSeed => roleSeed.Permissions.Select(permissionName =>
                (RoleId: roles[roleSeed.Name].RoleId, PermissionId: permissions[permissionName].PermissionId)))
            .ToHashSet();

        foreach (var assignment in existingAssignments)
        {
            if (seededRoleIds.Contains(assignment.RoleId)
                && !desiredAssignments.Contains((assignment.RoleId, assignment.PermissionId)))
            {
                repository.RemoveRolePermission(assignment);
            }
        }

        var existingAssignmentKeys = existingAssignments
            .Select(assignment => (assignment.RoleId, assignment.PermissionId))
            .ToHashSet();
        foreach (var (roleId, permissionId) in desiredAssignments)
        {
            if (!existingAssignmentKeys.Contains((roleId, permissionId)))
            {
                repository.AddRolePermission(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId
                });
            }
        }

        if (suppliedPasswordCount > 0)
        {
            foreach (var (roleName, username) in SeedUsernames)
            {
                var password = options.AccountPasswords[roleName]!;
                if (password.Length < 12)
                    throw new InvalidOperationException($"SeedAccounts:{roleName}:Password must be at least 12 characters.");

                if (await repository.EmployeeUsernameExistsAsync(username, cancellationToken))
                    continue;

                var employee = new Employee
                {
                    RoleId = roles[roleName].RoleId,
                    FullName = $"{roleName} account",
                    Username = username,
                    IsActive = true
                };
                employee.PasswordHash = passwordHasher.HashPassword(employee, password);
                repository.AddEmployee(employee);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}
