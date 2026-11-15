using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;

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
                e.CreatedAt))
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
            employee.CreatedAt);
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
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
        await repository.SaveChangesAsync(cancellationToken);

        return new EmployeeDto(
            employee.EmployeeId,
            employee.Username,
            employee.FullName,
            employee.Phone,
            role.RoleName,
            employee.IsActive,
            employee.CreatedAt);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(
        int employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
            employee.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Phone))
            employee.Phone = request.Phone.Trim();

        if (request.RoleId.HasValue)
        {
            var roles = await repository.GetRolesAsync(cancellationToken);
            var role = roles.FirstOrDefault(r => r.RoleId == request.RoleId);
            if (role is null)
                throw new InvalidOperationException("Vai trò không tồn tại.");
            employee.RoleId = request.RoleId.Value;
        }

        repository.UpdateEmployee(employee);
        await repository.SaveChangesAsync(cancellationToken);

        var updatedEmployee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể lấy thông tin nhân viên sau cập nhật.");

        return new EmployeeDto(
            updatedEmployee.EmployeeId,
            updatedEmployee.Username,
            updatedEmployee.FullName,
            updatedEmployee.Phone,
            updatedEmployee.Role.RoleName,
            updatedEmployee.IsActive,
            updatedEmployee.CreatedAt);
    }

    public async Task<EmployeeDto> LockEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        if (!employee.IsActive)
            throw new InvalidOperationException("Nhân viên đã bị khóa.");

        employee.IsActive = false;
        repository.UpdateEmployee(employee);
        await repository.SaveChangesAsync(cancellationToken);

        var lockedEmployee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể lấy thông tin nhân viên sau khóa.");

        return new EmployeeDto(
            lockedEmployee.EmployeeId,
            lockedEmployee.Username,
            lockedEmployee.FullName,
            lockedEmployee.Phone,
            lockedEmployee.Role.RoleName,
            lockedEmployee.IsActive,
            lockedEmployee.CreatedAt);
    }

    public async Task<EmployeeDto> UnlockEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        if (employee.IsActive)
            throw new InvalidOperationException("Nhân viên không bị khóa.");

        employee.IsActive = true;
        repository.UpdateEmployee(employee);
        await repository.SaveChangesAsync(cancellationToken);

        var unlockedEmployee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể lấy thông tin nhân viên sau mở khóa.");

        return new EmployeeDto(
            unlockedEmployee.EmployeeId,
            unlockedEmployee.Username,
            unlockedEmployee.FullName,
            unlockedEmployee.Phone,
            unlockedEmployee.Role.RoleName,
            unlockedEmployee.IsActive,
            unlockedEmployee.CreatedAt);
    }

    public async Task ChangePasswordAsync(
        int employeeId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        var verification = passwordHasher.VerifyHashedPassword(
            employee, employee.PasswordHash, request.CurrentPassword);
        
        if (verification == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Mật khẩu hiện tại không chính xác.");

        employee.PasswordHash = passwordHasher.HashPassword(employee, request.NewPassword);
        repository.UpdateEmployee(employee);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
