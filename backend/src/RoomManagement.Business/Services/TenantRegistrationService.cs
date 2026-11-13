using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Mappings;
using RoomManagement.Business.Validators;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class TenantRegistrationService : ITenantRegistrationService
{
    private readonly ITenantRegistrationStore store;
    private readonly IPasswordHasher<AppUser> hasher;
    private readonly ISmsOtpSender sms;
    private readonly PasswordHasher<TenantPhoneChallenge> codeHasher = new();

    public TenantRegistrationService(ITenantRegistrationStore store, IPasswordHasher<AppUser> hasher, ISmsOtpSender sms)
    {
        this.store = store;
        this.hasher = hasher;
        this.sms = sms;
    }

    public async Task<string> CheckAsync(string landlordCode, string phone, CancellationToken ct = default)
    {
        var (landlord, number) = await ResolveAsync(landlordCode, phone, ct);
        if (landlord is null) return "profile_not_found";
        var profiles = await store.FindByPhoneAsync(landlord.Id, number, ct);
        if (profiles.Count == 0) return "profile_not_found";
        if (profiles.Count > 1) return "ambiguous";
        if (profiles[0].UserId is not null || await store.UserNameExistsAsync(LoginName(landlord.LandlordCode!, number), ct))
            return "already_registered";
        return "eligible";
    }

    public async Task<Guid> RequestOtpAsync(string landlordCode, string phone, CancellationToken ct = default)
    {
        if (await CheckAsync(landlordCode, phone, ct) != "eligible")
            throw new AppConflictException("Không tìm thấy hồ sơ đủ điều kiện đăng ký.", "PROFILE_NOT_ELIGIBLE");
        var (landlord, number) = await ResolveAsync(landlordCode, phone, ct);
        var now = DateTime.UtcNow;
        var latest = await store.LatestChallengeAsync(landlord!.Id, number, ct);
        if (latest.HasValue && now - latest.Value < TimeSpan.FromSeconds(60))
            throw new AppConflictException("Vui lòng chờ 60 giây trước khi gửi mã mới.", "OTP_COOLDOWN");
        var code = sms.GenerateCode();
        var challenge = new TenantPhoneChallenge
        {
            Id = Guid.NewGuid(), LandlordId = landlord.Id, PhoneNumber = number,
            CreatedAt = now, ExpiresAt = now.AddMinutes(5)
        };
        challenge.CodeHash = codeHasher.HashPassword(challenge, code);
        await sms.SendAsync(number, code, ct);
        await store.AddChallengeAsync(challenge, ct);
        await store.SaveAsync(ct);
        return challenge.Id;
    }

    public async Task<Guid> VerifyOtpAsync(string landlordCode, string phone, Guid challengeId, string code, CancellationToken ct = default)
    {
        var (landlord, number) = await ResolveAsync(landlordCode, phone, ct);
        var challenge = await store.FindChallengeAsync(challengeId, ct);
        if (landlord is null || challenge is null || challenge.LandlordId != landlord.Id || challenge.PhoneNumber != number ||
            challenge.ConsumedAt is not null || challenge.ExpiresAt <= DateTime.UtcNow || challenge.Attempts >= 5)
            throw new AppValidationException("Mã xác minh không hợp lệ hoặc đã hết hạn.");
        challenge.Attempts++;
        if (codeHasher.VerifyHashedPassword(challenge, challenge.CodeHash, code ?? "") == PasswordVerificationResult.Failed)
        {
            await store.SaveAsync(ct);
            throw new AppValidationException("Mã xác minh không đúng.");
        }
        challenge.VerifiedAt = DateTime.UtcNow;
        await store.SaveAsync(ct);
        return challenge.Id;
    }

    public async Task<UserDto> RegisterAsync(string landlordCode, string phone, Guid challengeId, string password, string confirmPassword, CancellationToken ct = default)
    {
        var (landlord, number) = await ResolveAsync(landlordCode, phone, ct);
        if (landlord is null) throw new AppConflictException("Không tìm thấy hồ sơ đủ điều kiện đăng ký.", "PROFILE_NOT_FOUND");
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            throw new AppValidationException("Mật khẩu phải có ít nhất 6 ký tự.");
        if (password != confirmPassword)
            throw new AppValidationException("Mật khẩu nhập lại không khớp.");
        var login = LoginName(landlord.LandlordCode!, number);
        AppUser? created = null;
        await store.RegisterAsync(landlord.Id, login, number, challengeId, profile =>
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString(), UserName = login, NormalizedUserName = login,
                PhoneNumber = number, FullName = profile.FullName, Role = UserRoles.Tenant,
                IsActive = true, ApprovalStatus = ApprovalStatuses.Approved, CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            created = user;
            return user;
        }, ct);
        return created!.ToUserDto();
    }

    public async Task<UserDto> SelfRegisterAsync(SelfRegisterTenantDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Password) || dto.Password.Length < 6)
            throw new AppValidationException("Mật khẩu phải có ít nhất 6 ký tự.");
        if (dto.Password != dto.ConfirmPassword)
            throw new AppValidationException("Mật khẩu nhập lại không khớp.");

        var normalizedPhone = NormalizePhone(dto.PhoneNumber);
        if (!Regex.IsMatch(normalizedPhone, "^0[0-9]{9}$"))
            throw new AppValidationException("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");

        var normalizedCitizenId = (dto.CitizenId ?? string.Empty).Trim();
        var userName = string.IsNullOrWhiteSpace(dto.UserName) ? normalizedPhone : dto.UserName.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        if (normalizedEmail is not null)
            UserAccountRules.EnsureValidEmail(normalizedEmail);

        Gender? parsedGender = null;
        if (!string.IsNullOrWhiteSpace(dto.Gender))
        {
            if (!Enum.TryParse<Gender>(dto.Gender.Trim(), ignoreCase: true, out var g))
                throw new AppValidationException("Giới tính không hợp lệ (Male, Female, Other).");
            parsedGender = g;
        }

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail?.ToUpperInvariant(),
            Role = UserRoles.Tenant,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            IdentityVerificationStatus = IdentityVerificationStatuses.Unverified,
            IdentityVerifiedAt = null,
            FullName = string.IsNullOrWhiteSpace(dto.FullName) ? null : dto.FullName.Trim(),
            PhoneNumber = normalizedPhone,
            CitizenId = string.IsNullOrWhiteSpace(normalizedCitizenId) ? null : normalizedCitizenId,
            DateOfBirth = dto.DateOfBirth,
            Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
            CreatedAt = now
        };

        UserProfileRules.EnsureRequiredFieldsFilled(user);
        UserProfileRules.EnsureValidFormat(user);
        user.PasswordHash = hasher.HashPassword(user, dto.Password);

        var profile = new TenantProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OwnerLandlordUserId = null,
            CreatedByUserId = user.Id,
            FullName = user.FullName!,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            CitizenId = user.CitizenId,
            DateOfBirth = user.DateOfBirth,
            Gender = parsedGender,
            PermanentAddress = user.Address,
            CreatedAt = now,
            UpdatedAt = null
        };

        await store.SelfRegisterAsync(user, profile, ct);
        return user.ToUserDto();
    }

    public static string NormalizePhone(string? phone)
    {
        var trimmed = (phone ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (trimmed.StartsWith("+84", StringComparison.Ordinal))
            trimmed = "0" + trimmed[3..];
        else if (trimmed.StartsWith("84", StringComparison.Ordinal) && trimmed.Length == 11)
            trimmed = "0" + trimmed[2..];
        return trimmed;
    }

    private async Task<(AppUser? Landlord, string Phone)> ResolveAsync(string code, string phone, CancellationToken ct)
    {
        var number = phone?.Trim() ?? "";
        if (!Regex.IsMatch(number, "^0[0-9]{9}$"))
            throw new AppValidationException("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
        var normalizedCode = code?.Trim().ToUpperInvariant() ?? "";
        if (!Regex.IsMatch(normalizedCode, "^L[0-9A-Z]{9}$"))
            throw new AppValidationException("Mã chủ trọ không hợp lệ.");
        return (await store.FindLandlordAsync(normalizedCode, ct), number);
    }

    public static string LoginName(string landlordCode, string phone) => $"{landlordCode}:{phone}";
}
