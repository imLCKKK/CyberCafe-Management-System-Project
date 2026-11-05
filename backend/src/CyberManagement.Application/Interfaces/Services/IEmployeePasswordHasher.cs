using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Services;

public interface IEmployeePasswordHasher
{
    string HashPassword(Employee employee, string password);

    PasswordVerificationResult VerifyHashedPassword(
        Employee employee,
        string hashedPassword,
        string providedPassword);
}

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}
