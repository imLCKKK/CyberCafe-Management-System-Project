using CyberCafe.Api.Data;
using CyberCafe.Api.Features.Inventory;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Đọc cấu hình kết nối trước khi sử dụng.
var connectionString =
    builder.Configuration.GetConnectionString("CyberCafe");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<CyberCafeDbContext>(
        options => options.UseSqlServer(connectionString));

    // Đăng ký truy vấn kho.
    builder.Services.AddScoped<InventoryQueries>();
}

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// API kho sẽ được bật khi nối cấu hình xác thực chung.
app.Run();

public partial class Program;