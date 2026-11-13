using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoomManagement.Business.Interfaces;
using RoomManagement.Data.Stores;

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
        "Configure ConnectionStrings:RoomManagement in appsettings.Local.json or an environment variable.");
    }

    services.AddDbContext<RoomManagementDbContext>(options => options.UseSqlServer(connectionString));
    services.AddScoped<IUserStore, UserStore>();
    services.AddScoped<ILandlordApprovalStore, LandlordApprovalStore>();
    services.AddScoped<ITenantProfileStore, TenantProfileStore>();
    services.AddScoped<ITenantRegistrationStore, TenantRegistrationStore>();
    services.AddScoped<ISmsOtpSender, SmsOtpSender>();

    // Lấy thông tin admin từ appsettings
    services.Configure<Seeders.InitialAdminOptions>(configuration.GetSection(Seeders.InitialAdminOptions.SectionName));

    // Đăng ký IPasswordHasher để xử lý mật khẩu
    services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Entities.AppUser>, Microsoft.AspNetCore.Identity.PasswordHasher<Domain.Entities.AppUser>>();

    // Đăng ký AdminUserSeeder
    services.AddScoped<Seeders.AdminUserSeeder>();

    services.AddScoped<ILandlordStore, LandlordStore>();
    services.AddScoped<IPropertyStore, PropertyStore>();
    services.AddScoped<IRoomStore, RoomStore>();
    services.AddScoped<IRoomImageStore, RoomImageStore>();
    services.AddSingleton<IObjectStorage, AzureBlobObjectStorage>();
    services.AddScoped<IRoomBatchStore, RoomBatchStore>();
    services.AddScoped<IContractStore, ContractStore>();
    services.AddScoped<IContractRulesStore, ContractRulesStore>();
    services.AddScoped<IContractRulesEvidenceStore, ContractRulesEvidenceStore>();
    services.AddScoped<IBillingInputStore, BillingInputStore>();
    services.AddScoped<IInvoiceStore, InvoiceStore>();
    services.AddScoped<IPaymentStore, PaymentStore>();
    services.AddScoped<IPaymentEvidenceStore, PaymentEvidenceStore>();
    services.AddScoped<IRequestStore, RequestStore>();
    return services;
  }
}
