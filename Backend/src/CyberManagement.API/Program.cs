using CyberManagement.Infrastructure;
using CyberManagement.Application;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.API.Realtime;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Đăng ký service Application và các thành phần HTTP; repository do Infrastructure cung cấp.
            builder.Services.AddApplicationServices();
            builder.Services.AddApiServices(builder.Configuration);
            builder.Services.AddInfrastructureServices(builder.Configuration);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
            {
                await using var scope = app.Services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await database.Database.MigrateAsync();
                var accountPasswords = RolePermissionCatalog.Roles.ToDictionary(
                    role => role.Name,
                    role => app.Configuration[$"SeedAccounts:{role.Name}:Password"],
                    StringComparer.Ordinal);
                await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(
                    new IdentitySeedOptions(app.Environment.IsDevelopment(), accountPasswords),
                    CancellationToken.None);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Xử lý lỗi thống nhất trước khi request đi qua xác thực và controller.
            app.UseExceptionHandler();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
            app.MapControllers();
            app.MapHub<RealtimeHub>(RealtimeHub.Endpoint, options => options.CloseOnAuthenticationExpiration = true);

            await app.RunAsync();
        }
    }
}
