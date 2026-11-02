using RoomManagement.API.Contracts.Users;

namespace RoomManagement.API.Contracts.Auth;

/// <summary>
/// DTO phản hồi sau khi người dùng đăng nhập thành công.
/// Tuân thủ quy định tại ai-feature-workflow-guide.md về chuẩn Envelope { data }
/// đồng thời duy trì tính tương thích ngược với các trường ở root level.
/// </summary>
public sealed class LoginResponse
{
    public string Message { get; init; } = "Đăng nhập thành công.";
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTimeOffset ExpiresAt { get; init; }
    public UserResponse User { get; init; } = new();

    /// <summary>
    /// Thuộc tính bọc dữ liệu theo chuẩn envelope { data } của kiến trúc ai-feature-workflow-guide.md.
    /// Giúp frontend có thể truy cập qua response.data hoặc trực tiếp từ root.
    /// </summary>
    public LoginData Data => new(AccessToken, TokenType, ExpiresAt, User);
}

/// <summary>
/// Bản ghi chứa dữ liệu phiên đăng nhập nằm bên trong envelope { data }.
/// </summary>
public sealed record LoginData(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    UserResponse User);
