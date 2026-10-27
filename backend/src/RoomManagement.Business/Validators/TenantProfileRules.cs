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
    public const int MaxFullNameLength = 150;
    public const int MaxPhoneNumberLength = 20;
    public const int MaxEmailLength = 150;
    public const int MaxPermanentAddressLength = 300;

    // SĐT Việt Nam: 10 chữ số, bắt đầu bằng số 0 (ví dụ: 0912345678)
    private static readonly Regex PhoneNumberPattern = new(@"^0\d{9}$", RegexOptions.Compiled);

    // Căn cước công dân: đúng 12 chữ số
    private static readonly Regex CitizenIdPattern = new(@"^\d{12}$", RegexOptions.Compiled);

    /// <summary>
    /// Kiểm tra tính hợp lệ dữ liệu khi tạo mới hồ sơ người thuê.
    /// </summary>
    public static void Validate(CreateTenantProfileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
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
        ArgumentNullException.ThrowIfNull(dto);
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
        // 1. Họ và tên
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new AppValidationException("Họ và tên là bắt buộc và không được để trống.");
        }

        var trimmedFullName = fullName.Trim();
        if (trimmedFullName.Length > MaxFullNameLength)
        {
            throw new AppValidationException($"Họ và tên không được vượt quá {MaxFullNameLength} ký tự.");
        }

        // 2. Số điện thoại
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new AppValidationException("Số điện thoại là bắt buộc và không được để trống.");
        }

        var trimmedPhone = phoneNumber.Trim();
        if (trimmedPhone.Length > MaxPhoneNumberLength || !PhoneNumberPattern.IsMatch(trimmedPhone))
        {
            throw new AppValidationException("Số điện thoại không hợp lệ. Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");
        }

        // 3. Email (tùy chọn)
        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmedEmail = email.Trim();
            if (trimmedEmail.Length > MaxEmailLength || !new EmailAddressAttribute().IsValid(trimmedEmail))
            {
                throw new AppValidationException($"Email không đúng định dạng hoặc vượt quá {MaxEmailLength} ký tự.");
            }
        }

        // 4. CCCD / CMND (tùy chọn)
        if (!string.IsNullOrWhiteSpace(citizenId))
        {
            var trimmedCitizenId = citizenId.Trim();
            if (!CitizenIdPattern.IsMatch(trimmedCitizenId))
            {
                throw new AppValidationException("Số CCCD không hợp lệ. Số CCCD phải gồm đúng 12 chữ số.");
            }
        }

        // 5. Ngày sinh (tùy chọn)
        if (dateOfBirth is { } dob)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (dob >= today)
            {
                throw new AppValidationException("Ngày sinh phải là một ngày trong quá khứ.");
            }
        }

        // 6. Giới tính (tùy chọn)
        if (!string.IsNullOrWhiteSpace(gender))
        {
            var trimmedGender = gender.Trim();
            if (!TenantGenders.All.Contains(trimmedGender))
            {
                throw new AppValidationException("Giới tính không hợp lệ. Giới tính phải là Male, Female hoặc Other.");
            }
        }

        // 7. Địa chỉ thường trú (tùy chọn)
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
