using RoomManagement.Business.DTOs.Users;

namespace RoomManagement.Business.Interfaces;

public interface ITenantRegistrationService
{
    Task<string> CheckAsync(string landlordCode, string phone, CancellationToken ct = default);
    Task<Guid> RequestOtpAsync(string landlordCode, string phone, CancellationToken ct = default);
    Task<Guid> VerifyOtpAsync(string landlordCode, string phone, Guid challengeId, string code, CancellationToken ct = default);
    Task<UserDto> RegisterAsync(string landlordCode, string phone, Guid challengeId, string password, string confirmPassword, CancellationToken ct = default);
}
