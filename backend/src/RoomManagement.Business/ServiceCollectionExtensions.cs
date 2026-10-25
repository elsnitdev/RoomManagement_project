using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Business.Stores;

namespace RoomManagement.Business;

/// <summary>
/// Lớp mở rộng đăng ký các dịch vụ thuộc tầng Business vào DI Container.
/// Giúp giữ cho Program.cs gọn gàng, tuân thủ nguyên tắc Clean Architecture.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomManagementBusiness(this IServiceCollection services)
    {
        // Đăng ký các Store truy cập dữ liệu (Stores)
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<ILandlordApprovalStore, LandlordApprovalStore>();

        // Đăng ký các Service xử lý nghiệp vụ (Services)
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILandlordApprovalService, LandlordApprovalService>();

        return services;
    }
}
