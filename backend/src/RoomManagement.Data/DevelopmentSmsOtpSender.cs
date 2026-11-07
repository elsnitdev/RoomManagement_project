using RoomManagement.Business.Interfaces;

namespace RoomManagement.Data;

// Used only for local Development requests. The code is returned to that request's UI.
public sealed class DevelopmentSmsOtpSender : ISmsOtpSender
{
    public string GenerateCode() => "1111";
    public string? LastCode { get; private set; }

    public Task SendAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        LastCode = code;
        return Task.CompletedTask;
    }
}
