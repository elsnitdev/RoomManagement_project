using RoomManagement.Business;
using RoomManagement.Data;
using RoomManagement.API.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký tầng Data (DbContext, Seeder, PasswordHasher)
builder.Services.AddRoomManagementData(builder.Configuration);

// Đăng ký tầng Business (UserService và các nghiệp vụ khác)
builder.Services.AddRoomManagementBusiness();
builder.Services.AddRoomManagementAuthentication(builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();
// Validate authentication secrets before any bootstrap writes.
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

// 1. Xử lý Seed Data
try
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<RoomManagement.Data.Seeders.AdminUserSeeder>();
    // Tạo tài khoản Admin ban đầu
    await seeder.SeedAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Đã xảy ra lỗi trong quá trình khởi tạo dữ liệu ban đầu (Data Seeding).");
    throw;
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
