using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Data;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.API.Infrastructure;

public static class DevelopmentLandlordSeeder
{
  public static async Task SeedAsync(
      IServiceProvider services,
      IConfiguration configuration)
  {
    var id = configuration["DevelopmentLandlord:UserId"];
    if (string.IsNullOrWhiteSpace(id))
    {
      throw new InvalidOperationException(
          "Thiếu DevelopmentLandlord:UserId.");
    }

    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider
        .GetRequiredService<RoomManagementDbContext>();

    var existing = await db.Users
        .SingleOrDefaultAsync(u => u.Id == id);

    if (existing is not null)
    {
      if (existing.Role != UserRoles.Landlord || !existing.IsActive)
      {
        throw new InvalidOperationException(
            $"User '{id}' phải có role Landlord và đang hoạt động.");
      }

      return; // Chạy lại ứng dụng không tạo thêm user.
    }

    const string userName = "dev-landlord";
    var normalizedUserName = userName.ToUpperInvariant();

    if (await db.Users.AnyAsync(
            u => u.NormalizedUserName == normalizedUserName))
    {
      throw new InvalidOperationException(
          $"Username '{userName}' đã thuộc một user khác.");
    }

    var user = new AppUser
    {
      Id = id,
      UserName = userName,
      NormalizedUserName = normalizedUserName,
      Role = UserRoles.Landlord,
      IsActive = true,
      CreatedAt = DateTime.UtcNow
    };

    // Mật khẩu ngẫu nhiên không được lưu lại; user này chỉ phục vụ
    // FK và truy vấn Development, không dùng để đăng nhập.
    var randomPassword =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    user.PasswordHash = new PasswordHasher<AppUser>()
        .HashPassword(user, randomPassword);

    db.Users.Add(user);
    await db.SaveChangesAsync();
  }
}