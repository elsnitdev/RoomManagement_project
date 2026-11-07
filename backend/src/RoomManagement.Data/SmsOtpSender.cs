using RoomManagement.Business.Interfaces;
using System.Security.Cryptography;

namespace RoomManagement.Data;

// Replace this implementation when an SMS provider is configured. Never log or return the OTP.
public sealed class SmsOtpSender : ISmsOtpSender
{
    public string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    public Task SendAsync(string phoneNumber, string code, CancellationToken ct = default) =>
        throw new NotSupportedException("Dịch vụ xác minh SMS chưa được cấu hình.");
}
