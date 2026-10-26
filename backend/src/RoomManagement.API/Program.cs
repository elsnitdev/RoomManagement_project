using RoomManagement.Business;
using RoomManagement.Data;
using RoomManagement.API.Authentication;
using RoomManagement.API.Filters;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký dịch vụ ProblemDetails chuẩn RFC 7807 theo ai-feature-workflow-guide.md
builder.Services.AddProblemDetails();

// Đăng ký tầng Data (DbContext, Seeder, PasswordHasher, Stores)
builder.Services.AddRoomManagementData(builder.Configuration);

// Đăng ký tầng Business (UserService, LandlordApprovalService và các nghiệp vụ khác)
builder.Services.AddRoomManagementBusiness();
builder.Services.AddRoomManagementAuthentication(builder.Configuration);

// Đăng ký Controllers kèm GlobalExceptionFilter và cấu hình Enum dạng chuỗi (String Enum) theo ai-feature-workflow-guide.md
builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
})
.AddJsonOptions(options =>
{
    // Quy định tại ai-feature-workflow-guide.md: "Enum trao đổi qua JSON dạng chuỗi."
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

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
