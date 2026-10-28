using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Business.Exceptions;
using RoomManagement.Domain.Constants;

namespace RoomManagement.Business.Validators;

/// <summary>
/// Nơi định nghĩa các quy tắc kiểm tra tính hợp lệ dữ liệu của Hồ sơ người thuê (TenantProfile):
/// - FullName: Bắt buộc, không để trống, tối đa 150 ký tự.
/// - PhoneNumber: Bắt buộc, đúng định dạng số điện thoại Việt Nam (10 chữ số, bắt đầu bằng 0), tối đa 20 ký tự.
/// - Email: Tùy chọn, đúng định dạng email, tối đa 150 ký tự.
/// - CitizenId: Tùy chọn, đúng 12 chữ số.
/// - DateOfBirth: Tùy chọn, ngày sinh phải trong quá khứ.
/// - Gender: Tùy chọn, thuộc tập hợp TenantGenders.All (Male, Female, Other).
/// - PermanentAddress: Tùy chọn, tối đa 300 ký tự.
/// </summary>
public static class TenantProfileRules
{
    // Các hằng số giới hạn độ dài tối đa của chuỗi ký tự (khớp với độ dài cột dưới SQL Server)
    public const int MaxFullNameLength = 150;
    public const int MaxPhoneNumberLength = 20;
    public const int MaxEmailLength = 150;
    public const int MaxPermanentAddressLength = 300;

    // Biểu thức chính quy (Regex) kiểm tra SĐT Việt Nam: bắt đầu bằng '0' và theo sau là đúng 9 chữ số (\d{9})
    // RegexOptions.Compiled giúp biên dịch sẵn mẫu Regex để tăng tốc độ khi gọi kiểm tra nhiều lần
    private static readonly Regex PhoneNumberPattern = new(@"^0\d{9}$", RegexOptions.Compiled);

    // Biểu thức chính quy kiểm tra Căn cước công dân: phải gồm đúng 12 chữ số từ đầu đến cuối (^\d{12}$)
    private static readonly Regex CitizenIdPattern = new(@"^\d{12}$", RegexOptions.Compiled);

    /// <summary>
    /// Kiểm tra tính hợp lệ dữ liệu khi tạo mới hồ sơ người thuê.
    /// </summary>
    public static void Validate(CreateTenantProfileDto dto)
    {
        // Kiểm tra đối tượng dto truyền vào không được null (nếu null sẽ ném lỗi ArgumentNullException)
        ArgumentNullException.ThrowIfNull(dto);

        // Gọi hàm dùng chung để kiểm tra chi tiết từng trường
        ValidateFields(
            dto.FullName,
            dto.PhoneNumber,
            dto.Email,
            dto.CitizenId,
            dto.DateOfBirth,
            dto.Gender,
            dto.PermanentAddress);
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ dữ liệu khi cập nhật hồ sơ người thuê.
    /// </summary>
    public static void Validate(UpdateTenantProfileDto dto)
    {
        // Kiểm tra đối tượng dto truyền vào không được null
        ArgumentNullException.ThrowIfNull(dto);

        // Gọi hàm dùng chung để kiểm tra chi tiết từng trường
        ValidateFields(
            dto.FullName,
            dto.PhoneNumber,
            dto.Email,
            dto.CitizenId,
            dto.DateOfBirth,
            dto.Gender,
            dto.PermanentAddress);
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ chi tiết từng trường dữ liệu.
    /// Nếu có bất kỳ trường nào vi phạm, ném ra AppValidationException (sẽ được đổi thành HTTP 400 Bad Request ở tầng API).
    /// </summary>
    public static void ValidateFields(
        string? fullName,
        string? phoneNumber,
        string? email,
        string? citizenId,
        DateOnly? dateOfBirth,
        string? gender,
        string? permanentAddress)
    {
        // 1. Kiểm tra Họ và tên (Bắt buộc)
        // string.IsNullOrWhiteSpace: trả về true nếu chuỗi là null, rỗng "", hoặc chỉ toàn dấu cách "   "
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new AppValidationException("Họ và tên là bắt buộc và không được để trống.");
        }

        // Cắt khoảng trắng thừa ở 2 đầu trước khi đo độ dài
        var trimmedFullName = fullName.Trim();
        if (trimmedFullName.Length > MaxFullNameLength)
        {
            // Chuỗi nội suy $"..." trong C# cho phép chèn giá trị biến trực tiếp vào trong chuỗi
            throw new AppValidationException($"Họ và tên không được vượt quá {MaxFullNameLength} ký tự.");
        }

        // 2. Kiểm tra Số điện thoại (Bắt buộc)
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new AppValidationException("Số điện thoại là bắt buộc và không được để trống.");
        }

        var trimmedPhone = phoneNumber.Trim();
        // Kiểm tra độ dài hoặc không khớp biểu thức chính quy (dấu ! nghĩa là phủ định)
        if (trimmedPhone.Length > MaxPhoneNumberLength || !PhoneNumberPattern.IsMatch(trimmedPhone))
        {
            throw new AppValidationException("Số điện thoại không hợp lệ. Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");
        }

        // 3. Kiểm tra Email (Tùy chọn - chỉ kiểm tra khi người dùng có nhập dữ liệu)
        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmedEmail = email.Trim();
            // EmailAddressAttribute là công cụ có sẵn của .NET giúp kiểm tra định dạng email chuẩn
            if (trimmedEmail.Length > MaxEmailLength || !new EmailAddressAttribute().IsValid(trimmedEmail))
            {
                throw new AppValidationException($"Email không đúng định dạng hoặc vượt quá {MaxEmailLength} ký tự.");
            }
        }

        // 4. Kiểm tra CCCD / CMND (Tùy chọn - chỉ kiểm tra khi có nhập)
        if (!string.IsNullOrWhiteSpace(citizenId))
        {
            var trimmedCitizenId = citizenId.Trim();
            if (!CitizenIdPattern.IsMatch(trimmedCitizenId))
            {
                throw new AppValidationException("Số CCCD không hợp lệ. Số CCCD phải gồm đúng 12 chữ số.");
            }
        }

        // 5. Kiểm tra Ngày sinh (Tùy chọn - chỉ kiểm tra khi có giá trị khác null)
        // Cú pháp Pattern Matching 'is { } dob': nếu dateOfBirth khác null thì lấy giá trị gán vào biến 'dob'
        if (dateOfBirth is { } dob)
        {
            // Lấy ngày hiện tại theo múi giờ chuẩn UTC và chuyển sang kiểu DateOnly
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (dob >= today)
            {
                throw new AppValidationException("Ngày sinh phải là một ngày trong quá khứ.");
            }
        }

        // 6. Kiểm tra Giới tính (Tùy chọn - chỉ kiểm tra khi có nhập)
        if (!string.IsNullOrWhiteSpace(gender))
        {
            var trimmedGender = gender.Trim();
            // Kiểm tra giá trị có nằm trong tập hợp Male, Female, Other hay không
            if (!TenantGenders.All.Contains(trimmedGender))
            {
                throw new AppValidationException("Giới tính không hợp lệ. Giới tính phải là Male, Female hoặc Other.");
            }
        }

        // 7. Kiểm tra Địa chỉ thường trú (Tùy chọn - chỉ kiểm tra khi có nhập)
        if (!string.IsNullOrWhiteSpace(permanentAddress))
        {
            var trimmedAddress = permanentAddress.Trim();
            if (trimmedAddress.Length > MaxPermanentAddressLength)
            {
                throw new AppValidationException($"Địa chỉ thường trú không được vượt quá {MaxPermanentAddressLength} ký tự.");
            }
        }
    }
}
