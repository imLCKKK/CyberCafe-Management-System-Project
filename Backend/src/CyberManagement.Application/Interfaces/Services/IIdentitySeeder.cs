using CyberManagement.Application.DTOs.Identity;

namespace CyberManagement.Application.Interfaces.Services;

public interface IIdentitySeeder
{
    Task SeedAsync(IdentitySeedOptions options, CancellationToken cancellationToken);
}
