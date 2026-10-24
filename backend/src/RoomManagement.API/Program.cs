using RoomManagement.Business;
using RoomManagement.Data;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký tầng Data (DbContext, Seeder, PasswordHasher)
builder.Services.AddRoomManagementData(builder.Configuration);

// Đăng ký tầng Business (UserService và các nghiệp vụ khác)
builder.Services.AddRoomManagementBusiness();

builder.Services.AddControllers();

var app = builder.Build();

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
}

app.MapControllers();
app.Run();
