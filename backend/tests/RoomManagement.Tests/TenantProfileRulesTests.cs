using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Validators;

namespace RoomManagement.Tests;

/// <summary>
/// Unit Tests kiểm tra các quy tắc kiểm tra tính hợp lệ dữ liệu của Hồ sơ người thuê (TenantProfileRules).
/// </summary>
public sealed class TenantProfileRulesTests
{
    [Fact]
    public void Full_valid_data_passes_validation()
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Nguyễn Văn An",
            PhoneNumber = "0912345678",
            Email = "nguyenvanan@example.com",
            CitizenId = "079201012345",
            DateOfBirth = new DateOnly(2000, 1, 1),
            Gender = "Male",
            PermanentAddress = "123 Đường Nguyễn Huệ, Quận 1, TP.HCM"
        };

        var exception = Record.Exception(() => TenantProfileRules.Validate(dto));
        Assert.Null(exception);
    }

    [Fact]
    public void Minimum_valid_data_only_fullname_and_phone_passes()
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Trần Thị Bích",
            PhoneNumber = "0987654321"
        };

        var exception = Record.Exception(() => TenantProfileRules.Validate(dto));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_whitespace_fullname_throws_validation_exception(string? invalidName)
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = invalidName!,
            PhoneNumber = "0912345678"
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("Họ và tên là bắt buộc", ex.Message);
    }

    [Fact]
    public void Overlong_fullname_throws_validation_exception()
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = new string('A', 151),
            PhoneNumber = "0912345678"
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("150 ký tự", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_whitespace_phone_throws_validation_exception(string? invalidPhone)
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Lê Văn C",
            PhoneNumber = invalidPhone!
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("Số điện thoại là bắt buộc", ex.Message);
    }

    [Theory]
    [InlineData("1234567890")]   // Không bắt đầu bằng 0
    [InlineData("091234567")]    // 9 số (thiếu)
    [InlineData("09123456789")]  // 11 số (thừa)
    [InlineData("091234567a")]   // Chứa chữ
    [InlineData("abcdefghij")]   // Toàn chữ
    public void Invalid_phone_format_throws_validation_exception(string invalidPhone)
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Lê Văn C",
            PhoneNumber = invalidPhone
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("Số điện thoại không hợp lệ", ex.Message);
    }

    [Theory]
    [InlineData("12345678901")]    // 11 số
    [InlineData("1234567890123")]  // 13 số
    [InlineData("07920101234a")]   // Chứa chữ
    [InlineData("abcdefghijkl")]   // Toàn chữ
    public void Invalid_citizen_id_format_throws_validation_exception(string invalidCitizenId)
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Phạm Văn D",
            PhoneNumber = "0901234567",
            CitizenId = invalidCitizenId
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("Số CCCD không hợp lệ", ex.Message);
    }

    [Fact]
    public void Future_date_of_birth_throws_validation_exception()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var dto = new CreateTenantProfileDto
        {
            FullName = "Hoàng Văn E",
            PhoneNumber = "0901234567",
            DateOfBirth = futureDate
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("Ngày sinh phải là một ngày trong quá khứ", ex.Message);
    }

    [Theory]
    [InlineData("Male")]
    [InlineData("male")]
    [InlineData("Female")]
    [InlineData("female")]
    [InlineData("Other")]
    [InlineData("OTHER")]
    public void Valid_gender_case_insensitive_passes(string validGender)
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Đỗ Thị G",
            PhoneNumber = "0901234567",
            Gender = validGender
        };

        var exception = Record.Exception(() => TenantProfileRules.Validate(dto));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Nam")]
    [InlineData("Nu")]
    [InlineData("Unknown")]
    [InlineData("Invalid")]
    public void Invalid_gender_throws_validation_exception(string invalidGender)
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Đỗ Thị G",
            PhoneNumber = "0901234567",
            Gender = invalidGender
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("Giới tính không hợp lệ", ex.Message);
    }

    [Fact]
    public void Overlong_permanent_address_throws_validation_exception()
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Vũ Văn H",
            PhoneNumber = "0901234567",
            PermanentAddress = new string('X', 301)
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("300 ký tự", ex.Message);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    public void Invalid_email_format_throws_validation_exception(string invalidEmail)
    {
        var dto = new CreateTenantProfileDto
        {
            FullName = "Vũ Văn H",
            PhoneNumber = "0901234567",
            Email = invalidEmail
        };

        var ex = Assert.Throws<AppValidationException>(() => TenantProfileRules.Validate(dto));
        Assert.Contains("Email không đúng định dạng", ex.Message);
    }
}
