using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.Services;

public sealed class EmployeesService(
    IIdentityRepository repository,
    IEmployeePasswordHasher passwordHasher)
{
    public async Task<IReadOnlyList<EmployeeDto>> GetAllEmployeesAsync(CancellationToken cancellationToken)
    {
        var employees = await repository.GetAllEmployeesAsync(cancellationToken);
        return employees
            .Select(e => new EmployeeDto(
                e.EmployeeId,
                e.Username,
                e.FullName,
                e.Phone,
                e.Role.RoleName,
                e.IsActive,
                e.CreatedAt,
                Convert.ToBase64String(e.Version)))
            .ToList()
            .AsReadOnly();
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int employeeId, CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken);
        if (employee is null)
            return null;

        return new EmployeeDto(
            employee.EmployeeId,
            employee.Username,
            employee.FullName,
            employee.Phone,
            employee.Role.RoleName,
            employee.IsActive,
            employee.CreatedAt,
            Convert.ToBase64String(employee.Version));
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        EmployeePasswordPolicy.Validate(request.Password);
        var username = request.Username.Trim();
        
        if (await repository.EmployeeUsernameExistsAsync(username, cancellationToken))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var roles = await repository.GetRolesAsync(cancellationToken);
        var role = roles.FirstOrDefault(r => r.RoleId == request.RoleId);
        if (role is null)
            throw new InvalidOperationException("Vai trò không tồn tại.");

        var employee = new Employee
        {
            Username = username,
            FullName = request.FullName,
            Phone = request.Phone,
            RoleId = request.RoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        employee.PasswordHash = passwordHasher.HashPassword(employee, request.Password);

        repository.AddEmployee(employee);
        await SaveWithAuditAsync(employee, currentEmployeeId, "Created", cancellationToken);

        return new EmployeeDto(
            employee.EmployeeId,
            employee.Username,
            employee.FullName,
            employee.Phone,
            role.RoleName,
            employee.IsActive,
            employee.CreatedAt,
            Convert.ToBase64String(employee.Version));
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(
        int employeeId,
        UpdateEmployeeRequest request,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var expectedVersion = new byte[8];
        if (request.ExpectedVersion is null
            || !Convert.TryFromBase64String(request.ExpectedVersion, expectedVersion, out var length)
            || length != expectedVersion.Length)
            throw new ValidationException("expectedVersion phải là rowversion Base64 8 byte.");

        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        if (!employee.Version.SequenceEqual(expectedVersion))
            throw new BusinessRuleException("VersionConflict: hồ sơ nhân viên đã thay đổi. Vui lòng tải lại.");

        var preserveAnotherActiveAdmin = false;
        if (!string.IsNullOrWhiteSpace(request.FullName))
            employee.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Phone))
            employee.Phone = request.Phone.Trim();

        if (request.RoleId.HasValue)
        {
            if (employeeId == currentEmployeeId)
                throw new InvalidOperationException("Không thể thay đổi quyền của chính mình.");

            var roles = await repository.GetRolesAsync(cancellationToken);
            var role = roles.FirstOrDefault(r => r.RoleId == request.RoleId);
            if (role is null)
                throw new InvalidOperationException("Vai trò không tồn tại.");

            var targetRoleName = role.RoleName;
            var currentRoleName = employee.Role?.RoleName;
            preserveAnotherActiveAdmin = employee.IsActive
                && currentRoleName == "Admin"
                && targetRoleName != "Admin";

            employee.RoleId = request.RoleId.Value;
        }

        repository.UpdateEmployee(employee);
        await SaveWithAuditAsync(
            employee, currentEmployeeId, "Updated", cancellationToken, preserveAnotherActiveAdmin);

        var updatedEmployee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể lấy thông tin nhân viên sau cập nhật.");

        return new EmployeeDto(
            updatedEmployee.EmployeeId,
            updatedEmployee.Username,
            updatedEmployee.FullName,
            updatedEmployee.Phone,
            updatedEmployee.Role.RoleName,
            updatedEmployee.IsActive,
            updatedEmployee.CreatedAt,
            Convert.ToBase64String(updatedEmployee.Version));
    }

    public async Task<EmployeeDto> LockEmployeeAsync(
        int employeeId,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        if (employeeId == currentEmployeeId)
            throw new InvalidOperationException("Không thể khóa chính mình.");

        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        if (!employee.IsActive)
            throw new InvalidOperationException("Nhân viên đã bị khóa.");

        var preserveAnotherActiveAdmin = employee.Role?.RoleName == "Admin";

        employee.IsActive = false;
        repository.UpdateEmployee(employee);
        await SaveWithAuditAsync(
            employee, currentEmployeeId, "Locked", cancellationToken, preserveAnotherActiveAdmin);

        var lockedEmployee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể lấy thông tin nhân viên sau khóa.");

        return new EmployeeDto(
            lockedEmployee.EmployeeId,
            lockedEmployee.Username,
            lockedEmployee.FullName,
            lockedEmployee.Phone,
            lockedEmployee.Role.RoleName,
            lockedEmployee.IsActive,
            lockedEmployee.CreatedAt,
            Convert.ToBase64String(lockedEmployee.Version));
    }

    public async Task<EmployeeDto> UnlockEmployeeAsync(
        int employeeId,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        if (employee.IsActive)
            throw new InvalidOperationException("Nhân viên không bị khóa.");

        employee.IsActive = true;
        repository.UpdateEmployee(employee);
        await SaveWithAuditAsync(employee, currentEmployeeId, "Unlocked", cancellationToken);

        var unlockedEmployee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể lấy thông tin nhân viên sau mở khóa.");

        return new EmployeeDto(
            unlockedEmployee.EmployeeId,
            unlockedEmployee.Username,
            unlockedEmployee.FullName,
            unlockedEmployee.Phone,
            unlockedEmployee.Role.RoleName,
            unlockedEmployee.IsActive,
            unlockedEmployee.CreatedAt,
            Convert.ToBase64String(unlockedEmployee.Version));
    }

    public async Task ChangePasswordAsync(
        int employeeId,
        ChangePasswordRequest request,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        if (employeeId != currentEmployeeId)
            throw new UnauthorizedAccessException("Chỉ được đổi mật khẩu của chính mình.");

        EmployeePasswordPolicy.Validate(request.NewPassword);
        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
            throw new ValidationException("Xác nhận mật khẩu mới không khớp.");
        if (string.IsNullOrEmpty(request.CurrentPassword))
            throw new ValidationException("Vui lòng nhập mật khẩu hiện tại.");

        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        var verification = passwordHasher.VerifyHashedPassword(
            employee, employee.PasswordHash, request.CurrentPassword);
        
        if (verification == PasswordVerificationResult.Failed)
            throw new ValidationException("Mật khẩu hiện tại không chính xác.");

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            throw new ValidationException("Mật khẩu mới phải khác mật khẩu hiện tại.");

        employee.PasswordHash = passwordHasher.HashPassword(employee, request.NewPassword);
        repository.UpdateEmployee(employee);
        await SaveWithAuditAsync(employee, currentEmployeeId, "PasswordChanged", cancellationToken);
    }

    private Task SaveWithAuditAsync(
        Employee employee,
        int currentEmployeeId,
        string action,
        CancellationToken cancellationToken,
        bool preserveAnotherActiveAdmin = false) =>
        repository.SaveEmployeeWithAuditAsync(
            employee,
            new AuditLog
            {
                EmployeeId = currentEmployeeId,
                EntityType = "Employee",
                Action = action,
                CreatedAt = DateTime.UtcNow
            },
            preserveAnotherActiveAdmin,
            cancellationToken);
}
