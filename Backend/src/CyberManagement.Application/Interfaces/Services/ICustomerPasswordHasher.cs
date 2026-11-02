using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Services;

public interface ICustomerPasswordHasher
{
    string HashPassword(Customer customer, string password);

    PasswordVerificationResult VerifyHashedPassword(
        Customer customer,
        string hashedPassword,
        string providedPassword);
}
