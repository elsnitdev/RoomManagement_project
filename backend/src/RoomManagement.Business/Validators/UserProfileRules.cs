using System.Text.RegularExpressions;
using RoomManagement.Business.Exceptions;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Validators;

/// <summary>
/// Nơi DUY NHẤT định nghĩa quy tắc về thông tin cá nhân (profile):
///  - Vai trò nào bắt buộc những trường nào.
///  - Định dạng hợp lệ của số điện thoại, CCCD, ngày sinh.
/// Muốn thay đổi danh sách trường bắt buộc thì chỉ cần sửa ở file này.
/// </summary>
public static class UserProfileRules
{
    /// <summary>Tuổi tối thiểu để được đăng ký / ký hợp đồng.</summary>
    public const int MinimumAge = 18;

    // Số điện thoại Việt Nam: bắt đầu bằng 0, tổng cộng 10 chữ số (ví dụ: 0912345678)
    private static readonly Regex PhoneNumberPattern = new(@"^0\d{9}$", RegexOptions.Compiled);

    // Căn cước công dân: đúng 12 chữ số
    private static readonly Regex CitizenIdPattern = new(@"^\d{12}$", RegexOptions.Compiled);

    // Tên hiển thị tiếng Việt của từng trường, dùng để ghép câu thông báo lỗi cho dễ hiểu
    private static readonly Dictionary<string, string> FieldDisplayNames = new()
    {
        [nameof(AppUser.FullName)] = "Họ và tên",
        [nameof(AppUser.PhoneNumber)] = "Số điện thoại",
        [nameof(AppUser.CitizenId)] = "Số CCCD",
        [nameof(AppUser.DateOfBirth)] = "Ngày sinh",
        [nameof(AppUser.Address)] = "Địa chỉ thường trú"
    };

    /// <summary>
    /// Trả về danh sách tên các trường bắt buộc mà người dùng CHƯA điền.
    ///  - Landlord: Họ tên, SĐT, CCCD, Ngày sinh, Địa chỉ thường trú.
    ///  - Tenant:   Họ tên, SĐT, CCCD, Ngày sinh.
    ///  - Admin:    không bắt buộc gì.
    /// </summary>
    public static IReadOnlyList<string> GetMissingFields(AppUser user)
    {
        var missingFields = new List<string>();

        // Admin không cần hồ sơ cá nhân
        if (user.Role == UserRoles.Admin)
        {
            return missingFields;
        }

        // Các trường bắt buộc chung cho cả Landlord và Tenant
        if (string.IsNullOrWhiteSpace(user.FullName)) missingFields.Add(nameof(AppUser.FullName));
        if (string.IsNullOrWhiteSpace(user.PhoneNumber)) missingFields.Add(nameof(AppUser.PhoneNumber));
        if (string.IsNullOrWhiteSpace(user.CitizenId)) missingFields.Add(nameof(AppUser.CitizenId));
        if (user.DateOfBirth is null) missingFields.Add(nameof(AppUser.DateOfBirth));

        // Riêng chủ trọ phải có thêm địa chỉ thường trú
        if (user.Role == UserRoles.Landlord && string.IsNullOrWhiteSpace(user.Address))
        {
            missingFields.Add(nameof(AppUser.Address));
        }

        return missingFields;
    }

    /// <summary>
    /// Ném lỗi 400 nếu người dùng còn thiếu trường bắt buộc.
    /// Câu thông báo liệt kê rõ các trường còn thiếu bằng tiếng Việt.
    /// </summary>
    public static void EnsureRequiredFieldsFilled(AppUser user)
    {
        var missingFields = GetMissingFields(user);
        if (missingFields.Count == 0)
        {
            return;
        }

        // Đổi tên kỹ thuật (FullName...) sang tên tiếng Việt (Họ và tên...)
        var displayNames = missingFields.Select(field => FieldDisplayNames[field]);
        throw new AppValidationException($"Vui lòng nhập đầy đủ thông tin bắt buộc: {string.Join(", ", displayNames)}.");
    }

    /// <summary>
    /// Kiểm tra định dạng các trường đã được nhập (trường bỏ trống sẽ được bỏ qua,
    /// việc thiếu trường do <see cref="EnsureRequiredFieldsFilled"/> đảm nhận).
    /// </summary>
    public static void EnsureValidFormat(AppUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.PhoneNumber) && !PhoneNumberPattern.IsMatch(user.PhoneNumber))
        {
            throw new AppValidationException("Số điện thoại không hợp lệ. Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");
        }

        if (!string.IsNullOrWhiteSpace(user.CitizenId) && !CitizenIdPattern.IsMatch(user.CitizenId))
        {
            throw new AppValidationException("Số CCCD không hợp lệ. Số CCCD phải gồm đúng 12 chữ số.");
        }

        if (user.DateOfBirth is { } dateOfBirth)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Ngày sinh không được nằm ở tương lai
            if (dateOfBirth > today)
            {
                throw new AppValidationException("Ngày sinh không được lớn hơn ngày hiện tại.");
            }

            // Cộng thêm MinimumAge năm vào ngày sinh, nếu vẫn lớn hơn hôm nay => chưa đủ tuổi
            if (dateOfBirth.AddYears(MinimumAge) > today)
            {
                throw new AppValidationException($"Người dùng phải đủ {MinimumAge} tuổi trở lên.");
            }
        }
    }
}
