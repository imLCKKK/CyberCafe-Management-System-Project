using CyberCafe.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("CyberCafe");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<CyberCafeDbContext>(options => options.UseSqlServer(connectionString));
}

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.Run();

public partial class Program;
