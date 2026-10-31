using CyberManagement.Infrastructure;
using CyberManagement.Application;

namespace CyberManagement.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Đăng ký service Application và các thành phần HTTP; repository do Infrastructure cung cấp.
            builder.Services.AddApplicationServices();
            builder.Services.AddApiServices();
            builder.Services.AddInfrastructureServices(builder.Configuration);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
                
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


            app.MapControllers();

            app.Run();
        }
    }
}
