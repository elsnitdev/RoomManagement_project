namespace RoomManagement.Business.Interfaces;

public interface ISmsOtpSender
{
    string GenerateCode();
    Task SendAsync(string phoneNumber, string code, CancellationToken ct = default);
}
