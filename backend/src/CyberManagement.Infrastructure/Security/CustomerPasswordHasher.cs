using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using ApplicationPasswordVerificationResult = CyberManagement.Application.Interfaces.Services.PasswordVerificationResult;

namespace CyberManagement.Infrastructure.Security;

public sealed class CustomerPasswordHasher : ICustomerPasswordHasher
{
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public string HashPassword(Customer customer, string password) =>
        _passwordHasher.HashPassword(customer, password);

    public ApplicationPasswordVerificationResult VerifyHashedPassword(
        Customer customer,
        string hashedPassword,
        string providedPassword) =>
        _passwordHasher.VerifyHashedPassword(customer, hashedPassword, providedPassword) switch
        {
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed =>
                ApplicationPasswordVerificationResult.Failed,
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded =>
                ApplicationPasswordVerificationResult.SuccessRehashNeeded,
            _ => ApplicationPasswordVerificationResult.Success
        };
}
