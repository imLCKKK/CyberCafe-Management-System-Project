using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CyberManagement.Infrastructure.Security;

public sealed class EmployeePasswordHasher : IEmployeePasswordHasher
{
    private readonly PasswordHasher<Employee> _passwordHasher = new();

    public string HashPassword(Employee employee, string password) =>
        _passwordHasher.HashPassword(employee, password);

    public Application.Interfaces.Services.PasswordVerificationResult VerifyHashedPassword(
        Employee employee,
        string hashedPassword,
        string providedPassword) =>
        _passwordHasher.VerifyHashedPassword(employee, hashedPassword, providedPassword) switch
        {
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed =>
                Application.Interfaces.Services.PasswordVerificationResult.Failed,
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded =>
                Application.Interfaces.Services.PasswordVerificationResult.SuccessRehashNeeded,
            _ => Application.Interfaces.Services.PasswordVerificationResult.Success
        };
}
