using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RoomManagement.Data;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddRoomManagementData(
      this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("RoomManagement");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException(
          "Set ConnectionStrings:RoomManagement using environment variables or user secrets.");
    }

    services.AddDbContext<RoomManagementDbContext>(options => options.UseSqlServer(connectionString));

    // Lấy thông tin admin từ appsettings
    services.Configure<Seeders.InitialAdminOptions>(configuration.GetSection(Seeders.InitialAdminOptions.SectionName));

    // Đăng ký IPasswordHasher để xử lý mật khẩu
    services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Entities.AppUser>, Microsoft.AspNetCore.Identity.PasswordHasher<Domain.Entities.AppUser>>();

    // Đăng ký AdminUserSeeder
    services.AddScoped<Seeders.AdminUserSeeder>();

    return services;
  }
}
