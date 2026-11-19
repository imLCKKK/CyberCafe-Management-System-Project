using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json; 

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
        CancellationToken cancellationToken) =>
        await CreateEmployeeCoreAsync(request, currentEmployeeId, null, null, cancellationToken);

    private async Task<EmployeeDto> CreateEmployeeCoreAsync(
        CreateEmployeeRequest request,
        int currentEmployeeId,
        string? createRequestKey,
        string? createRequestFingerprint,
        CancellationToken cancellationToken)
    {
        EmployeePasswordPolicy.Validate(request.Password);
        var username = request.Username?.Trim() ?? "";
        var fullName = request.FullName?.Trim() ?? "";
        var phone = request.Phone?.Trim() ?? "";

        if (string.IsNullOrEmpty(username) || username.Length > 50)
            throw new ValidationException("Tên đăng nhập phải 1–50 ký tự không rỗng.");

        if (string.IsNullOrEmpty(fullName) || fullName.Length > 150)
            throw new ValidationException("Họ và tên phải 1–150 ký tự không rỗng.");

        if (phone.Length > 11)
            throw new ValidationException("Số điện thoại tối đa 11 ký tự.");

        if (await repository.EmployeeUsernameExistsAsync(username, cancellationToken))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var roles = await repository.GetRolesAsync(cancellationToken);
        var role = roles.FirstOrDefault(r => r.RoleId == request.RoleId);
        if (role is null)
            throw new InvalidOperationException("Vai trò không tồn tại.");

        var employee = new Employee
        {
            Username = username,
            FullName = fullName,
            Phone = phone.Length > 0 ? phone : null,
            RoleId = request.RoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreateRequestKey = createRequestKey,
            CreateRequestFingerprint = createRequestFingerprint
        };

        employee.PasswordHash = passwordHasher.HashPassword(employee, request.Password);
        repository.AddEmployee(employee);
        AuditLog? mutation = createRequestKey is null
            ? null
            : new AuditLog
            {
                EmployeeId = currentEmployeeId,
                EntityId = 0,
                EntityType = "Employee",
                Action = "Created",
                RequestKey = createRequestKey,
                RequestFingerprint = createRequestFingerprint,
                CreatedAt = DateTime.UtcNow
            };
        await SaveWithAuditAsync(
            employee,
            currentEmployeeId,
            "Created",
            cancellationToken,
            mutation: mutation,
            mutationResponse: saved => JsonSerializer.Serialize(ToDto(saved)));

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

    public Task<EmployeeDto> UpdateEmployeeAsync(
        int employeeId,
        UpdateEmployeeRequest request,
        int currentEmployeeId,
        CancellationToken cancellationToken) =>
        UpdateEmployeeCoreAsync(employeeId, request, currentEmployeeId, cancellationToken, null);

    private async Task<EmployeeDto> UpdateEmployeeCoreAsync(
        int employeeId,
        UpdateEmployeeRequest request,
        int currentEmployeeId,
        CancellationToken cancellationToken,
        AuditLog? mutation)
    {
        var expectedVersion = new byte[8];
        if (request.ExpectedVersion is null
            || !Convert.TryFromBase64String(request.ExpectedVersion, expectedVersion, out var length)
            || length != expectedVersion.Length)
            throw new ValidationException("expectedVersion phải là rowversion Base64 8 byte.");

        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        var preserveAnotherActiveAdmin = false;
        
        if (request.FullName is not null)
        {
            var fullName = request.FullName.Trim();
            if (fullName.Length > 0 && fullName.Length <= 150)
                employee.FullName = fullName;
            else if (fullName.Length > 150)
                throw new ValidationException("Họ và tên tối đa 150 ký tự.");
            else if (fullName.Length == 0)
                throw new ValidationException("Họ và tên không được để trống.");
        }

        if (request.Phone is not null)
        {
            var phone = request.Phone.Trim();
            if (phone.Length > 11)
                throw new ValidationException("Số điện thoại tối đa 11 ký tự.");
            employee.Phone = phone.Length > 0 ? phone : null;
        }

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
            employee,
            currentEmployeeId,
            "Updated",
            cancellationToken,
            preserveAnotherActiveAdmin,
            mutation,
            saved => JsonSerializer.Serialize(ToDto(saved)),
            expectedVersion);

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

    public Task<EmployeeDto> LockEmployeeAsync(
        int employeeId,
        int currentEmployeeId,
        CancellationToken cancellationToken) =>
        LockEmployeeCoreAsync(employeeId, currentEmployeeId, cancellationToken, null);

    private async Task<EmployeeDto> LockEmployeeCoreAsync(
        int employeeId,
        int currentEmployeeId,
        CancellationToken cancellationToken,
        AuditLog? mutation)
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
            employee,
            currentEmployeeId,
            "Locked",
            cancellationToken,
            preserveAnotherActiveAdmin,
            mutation,
            saved => JsonSerializer.Serialize(ToDto(saved)));

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

    public Task<EmployeeDto> UnlockEmployeeAsync(
        int employeeId,
        int currentEmployeeId,
        CancellationToken cancellationToken) =>
        UnlockEmployeeCoreAsync(employeeId, currentEmployeeId, cancellationToken, null);

    private async Task<EmployeeDto> UnlockEmployeeCoreAsync(
        int employeeId,
        int currentEmployeeId,
        CancellationToken cancellationToken,
        AuditLog? mutation)
    {
        var employee = await repository.GetEmployeeByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Nhân viên không tồn tại.");

        if (employee.IsActive)
            throw new InvalidOperationException("Nhân viên không bị khóa.");

        employee.IsActive = true;
        repository.UpdateEmployee(employee);
        await SaveWithAuditAsync(
            employee,
            currentEmployeeId,
            "Unlocked",
            cancellationToken,
            mutation: mutation,
            mutationResponse: saved => JsonSerializer.Serialize(ToDto(saved)));

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

    public Task ChangePasswordAsync(
        int employeeId,
        ChangePasswordRequest request,
        int currentEmployeeId,
        CancellationToken cancellationToken) =>
        ChangePasswordCoreAsync(employeeId, request, currentEmployeeId, cancellationToken, null);

    private async Task ChangePasswordCoreAsync(
        int employeeId,
        ChangePasswordRequest request,
        int currentEmployeeId,
        CancellationToken cancellationToken,
        AuditLog? mutation)
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
        await SaveWithAuditAsync(
            employee,
            currentEmployeeId,
            "PasswordChanged",
            cancellationToken,
            mutation: mutation,
            mutationResponse: _ => "{}");
    }

    private Task SaveWithAuditAsync(
        Employee employee,
        int currentEmployeeId,
        string action,
        CancellationToken cancellationToken,
        bool preserveAnotherActiveAdmin = false,
        AuditLog? mutation = null,
        Func<Employee, string>? mutationResponse = null,
        byte[]? expectedVersion = null)
    {
        var audit = mutation ?? new AuditLog();
        audit.EmployeeId = currentEmployeeId;
        audit.EntityType = "Employee";
        audit.Action = action;
        audit.CreatedAt = DateTime.UtcNow;
        return repository.SaveEmployeeWithAuditAsync(
            employee,
            audit,
            preserveAnotherActiveAdmin,
            cancellationToken,
            mutationResponse,
            expectedVersion);
    }
    
    public async Task<(EmployeeDto Employee, bool Replayed)> CreateEmployeeWithIdempotencyAsync(
        CreateEmployeeRequest request,
        string idempotencyKey,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var keyStr = idempotencyKey;
        var fingerprint = ComputeFingerprint(request);
        var existingMutation = await repository.GetEmployeeMutationAsync(
            currentEmployeeId, keyStr, cancellationToken);
        if (existingMutation is not null)
        {
            if (existingMutation.Action != "Created"
                || !string.Equals(existingMutation.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                throw new ConflictException("409: Idempotency-Key đã được sử dụng với thao tác hoặc payload khác.");
            return (Deserialize<EmployeeDto>(existingMutation.ResponseJson!), true);
        }

        var existing = await repository.GetEmployeeByCreateKeyAsync(keyStr, cancellationToken);
        if (existing is not null)
        {
            var existingFingerprint = existing.CreateRequestFingerprint ?? "";
            if (!string.Equals(fingerprint, existingFingerprint, StringComparison.Ordinal))
                throw new ConflictException("409: Idempotency-Key đã được sử dụng với payload khác.");

            return (new EmployeeDto(
                existing.EmployeeId,
                existing.Username,
                existing.FullName,
                existing.Phone,
                existing.Role.RoleName,
                existing.IsActive,
                existing.CreatedAt,
                Convert.ToBase64String(existing.Version)), true);
        }

        try
        {
            var employee = await CreateEmployeeCoreAsync(
                request, currentEmployeeId, keyStr, fingerprint, cancellationToken);
            return (employee, false);
        }
        catch (ConflictException)
        {
            var concurrentMutation = await repository.GetEmployeeMutationAsync(
                currentEmployeeId, keyStr, cancellationToken);
            if (concurrentMutation is not null)
            {
                if (concurrentMutation.Action != "Created"
                    || !string.Equals(fingerprint, concurrentMutation.RequestFingerprint, StringComparison.Ordinal))
                    throw new ConflictException("409: Idempotency-Key đã được sử dụng với thao tác hoặc payload khác.");
                return (Deserialize<EmployeeDto>(concurrentMutation.ResponseJson!), true);
            }

            var concurrentResult = await repository.GetEmployeeByCreateKeyAsync(keyStr, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy kết quả của yêu cầu idempotent đồng thời.");
            if (!string.Equals(fingerprint, concurrentResult.CreateRequestFingerprint, StringComparison.Ordinal))
                throw new ConflictException("409: Idempotency-Key đã được sử dụng với payload khác.");
            return (ToDto(concurrentResult), true);
        }
    }

    public async Task<(EmployeeDto Employee, bool Replayed)> UpdateEmployeeWithIdempotencyAsync(
        int employeeId,
        UpdateEmployeeRequest request,
        string idempotencyKey,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var mutation = await FindOrCreateMutationAsync(
            employeeId, currentEmployeeId, "Updated", request, idempotencyKey, cancellationToken);
        if (mutation.Existing is not null)
            return (Deserialize<EmployeeDto>(mutation.Existing.ResponseJson!), true);

        try
        {
            var employee = await UpdateEmployeeCoreAsync(
                employeeId, request, currentEmployeeId, cancellationToken, mutation.New);
            return (employee, false);
        }
        catch (ConflictException)
        {
            var replay = await GetReplayAsync<EmployeeDto>(
                currentEmployeeId, mutation.New!.RequestKey!, mutation.New.RequestFingerprint!, cancellationToken);
            return (replay, true);
        }
    }

    public async Task<(EmployeeDto Employee, bool Replayed)> LockEmployeeWithIdempotencyAsync(
        int employeeId,
        string idempotencyKey,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var mutation = await FindOrCreateMutationAsync(
            employeeId, currentEmployeeId, "Locked", new { }, idempotencyKey, cancellationToken);
        if (mutation.Existing is not null)
            return (Deserialize<EmployeeDto>(mutation.Existing.ResponseJson!), true);

        try
        {
            var employee = await LockEmployeeCoreAsync(
                employeeId, currentEmployeeId, cancellationToken, mutation.New);
            return (employee, false);
        }
        catch (ConflictException)
        {
            var replay = await GetReplayAsync<EmployeeDto>(
                currentEmployeeId, mutation.New!.RequestKey!, mutation.New.RequestFingerprint!, cancellationToken);
            return (replay, true);
        }
    }

    public async Task<(EmployeeDto Employee, bool Replayed)> UnlockEmployeeWithIdempotencyAsync(
        int employeeId,
        string idempotencyKey,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var mutation = await FindOrCreateMutationAsync(
            employeeId, currentEmployeeId, "Unlocked", new { }, idempotencyKey, cancellationToken);
        if (mutation.Existing is not null)
            return (Deserialize<EmployeeDto>(mutation.Existing.ResponseJson!), true);

        try
        {
            var employee = await UnlockEmployeeCoreAsync(
                employeeId, currentEmployeeId, cancellationToken, mutation.New);
            return (employee, false);
        }
        catch (ConflictException)
        {
            var replay = await GetReplayAsync<EmployeeDto>(
                currentEmployeeId, mutation.New!.RequestKey!, mutation.New.RequestFingerprint!, cancellationToken);
            return (replay, true);
        }
    }

    public async Task<bool> ChangePasswordWithIdempotencyAsync(
        int employeeId,
        ChangePasswordRequest request,
        string idempotencyKey,
        int currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var mutation = await FindOrCreateMutationAsync(
            employeeId, currentEmployeeId, "PasswordChanged", request, idempotencyKey, cancellationToken);
        if (mutation.Existing is not null)
            return true;

        try
        {
            await ChangePasswordCoreAsync(
                employeeId, request, currentEmployeeId, cancellationToken, mutation.New);
            return false;
        }
        catch (ConflictException)
        {
            await GetReplayAsync<JsonElement>(
                currentEmployeeId, mutation.New!.RequestKey!, mutation.New.RequestFingerprint!, cancellationToken);
            return true;
        }
    }

    private async Task<(AuditLog? Existing, AuditLog? New)> FindOrCreateMutationAsync(
        int employeeId,
        int actorEmployeeId,
        string operation,
        object request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestKey = idempotencyKey;
        var fingerprint = ComputeFingerprint(new { operation, employeeId, request });
        var existing = await repository.GetEmployeeMutationAsync(actorEmployeeId, requestKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                || existing.EntityId != employeeId
                || existing.Action != operation)
                throw new ConflictException("409: Idempotency-Key đã được sử dụng với thao tác hoặc payload khác.");
            return (existing, null);
        }

        return (null, new AuditLog
        {
            EntityType = "Employee",
            EntityId = employeeId,
            EmployeeId = actorEmployeeId,
            RequestKey = requestKey,
            Action = operation,
            RequestFingerprint = fingerprint,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<T> GetReplayAsync<T>(
        int actorEmployeeId,
        string requestKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetEmployeeMutationAsync(actorEmployeeId, requestKey, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy kết quả của yêu cầu idempotent đồng thời.");
        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new ConflictException("409: Idempotency-Key đã được sử dụng với thao tác hoặc payload khác.");
        return Deserialize<T>(existing.ResponseJson!);
    }

    private static EmployeeDto ToDto(Employee employee) => new(
        employee.EmployeeId,
        employee.Username,
        employee.FullName,
        employee.Phone,
        employee.Role.RoleName,
        employee.IsActive,
        employee.CreatedAt,
        Convert.ToBase64String(employee.Version));

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Kết quả idempotent đã lưu không hợp lệ.");

    private static string ComputeFingerprint(object request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToUpperInvariant();
    }

    private static string ComputeFingerprint(CreateEmployeeRequest request) => ComputeFingerprint((object)request);
}
