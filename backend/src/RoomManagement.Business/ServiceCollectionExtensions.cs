using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;

namespace RoomManagement.Business;

/// <summary>
/// Lớp mở rộng đăng ký các dịch vụ thuộc tầng Business vào DI Container.
/// Giúp giữ cho Program.cs gọn gàng, tuân thủ nguyên tắc Clean Architecture.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomManagementBusiness(this IServiceCollection services)
    {
        // Đăng ký UserService phục vụ nghiệp vụ quản lý người dùng
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
