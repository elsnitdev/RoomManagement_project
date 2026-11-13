using Microsoft.AspNetCore.Identity;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.DTOs.Managers;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Validators;
using RoomManagement.Domain;
using RoomManagement.Domain.Constants;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Business.Services;

public sealed class ManagerService : IManagerService
{
    private const int MinimumPasswordLength = 6;
    private readonly ICurrentLandlord _current;
    private readonly ILandlordStore _landlords;
    private readonly IManagerStore _store;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public ManagerService(
        ICurrentLandlord current,
        ILandlordStore landlords,
        IManagerStore store,
        IPasswordHasher<AppUser> passwordHasher)
    {
        _current = current;
        _landlords = landlords;
        _store = store;
        _passwordHasher = passwordHasher;
    }

    public async Task<ManagerDto> CreateAsync(CreateManagerInput input, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_current, _landlords, ct);
        var landlordId = context.OwnerLandlordUserId;

        var trimmedUserName = input.UserName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedUserName) || trimmedUserName.Length > 256)
            throw new BusinessException(BusinessErrorType.Validation, "USERNAME_INVALID", "Tên đăng nhập không hợp lệ.");

        if (string.IsNullOrEmpty(input.Password) || input.Password.Length < MinimumPasswordLength)
            throw new BusinessException(BusinessErrorType.Validation, "PASSWORD_TOO_SHORT", $"Mật khẩu tạm phải có ít nhất {MinimumPasswordLength} ký tự.");

        var trimmedEmail = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        if (trimmedEmail is not null)
        {
            try { UserAccountRules.EnsureValidEmail(trimmedEmail); }
            catch (Exception ex)
            {
                throw new BusinessException(BusinessErrorType.Validation, "EMAIL_INVALID", ex.Message, ex);
            }
        }

        var normalizedUserName = trimmedUserName.ToUpperInvariant();
        if (await _store.IsUserNameTakenAsync(normalizedUserName, ct))
            throw new BusinessException(BusinessErrorType.Conflict, "USERNAME_EXISTS", $"Tên đăng nhập '{trimmedUserName}' đã tồn tại trong hệ thống.");

        var normalizedEmail = trimmedEmail?.ToUpperInvariant();
        if (normalizedEmail is not null && await _store.IsEmailTakenAsync(normalizedEmail, ct))
            throw new BusinessException(BusinessErrorType.Conflict, "EMAIL_EXISTS", $"Email '{trimmedEmail}' đã được sử dụng.");

        var requestedPropertyIds = (input.PropertyIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (requestedPropertyIds.Count > 0)
        {
            var owned = await _store.GetOwnedPropertiesAsync(landlordId, requestedPropertyIds, ct);
            if (owned.Count != requestedPropertyIds.Count)
                throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Một hoặc nhiều khu trọ không tồn tại hoặc không thuộc quyền sở hữu của bạn.");
        }

        var now = DateTime.UtcNow;
        var manager = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = trimmedUserName,
            NormalizedUserName = normalizedUserName,
            Email = trimmedEmail,
            NormalizedEmail = normalizedEmail,
            FullName = string.IsNullOrWhiteSpace(input.FullName) ? null : input.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim(),
            Role = UserRoles.Manager,
            LandlordUserId = landlordId,
            IsActive = true,
            MustChangePassword = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            CreatedAt = now
        };
        manager.PasswordHash = _passwordHasher.HashPassword(manager, input.Password);

        var assignments = requestedPropertyIds.Select(propertyId => new PropertyManagerAssignment
        {
            Id = Guid.NewGuid(),
            ManagerUserId = manager.Id,
            PropertyId = propertyId,
            AssignedByUserId = context.ActorUserId,
            AssignedAt = now
        }).ToList();

        await _store.CreateManagerAsync(manager, assignments, ct);

        return await _store.GetManagerDetailsAsync(landlordId, manager.Id, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "MANAGER_NOT_FOUND", "Không tìm thấy tài khoản quản lý.");
    }

    public async Task<PageResult<ManagerDto>> ListAsync(ManagerFilter filter, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_current, _landlords, ct);
        PropertyRoomValidation.Page(filter.Page, filter.PageSize);
        PropertyRoomValidation.Search(filter.Search);

        var (items, total) = await _store.ListManagersAsync(
            context.OwnerLandlordUserId,
            filter.Search?.Trim(),
            filter.IsActive,
            (filter.Page - 1) * filter.PageSize,
            filter.PageSize,
            ct);

        return new PageResult<ManagerDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<ManagerDto> GetAsync(string managerId, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_current, _landlords, ct);
        return await _store.GetManagerDetailsAsync(context.OwnerLandlordUserId, managerId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "MANAGER_NOT_FOUND", "Không tìm thấy tài khoản quản lý.");
    }

    public async Task<ManagerDto> AssignPropertiesAsync(string managerId, IReadOnlyList<Guid> propertyIds, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_current, _landlords, ct);
        var landlordId = context.OwnerLandlordUserId;

        var manager = await _store.FindManagerAsync(landlordId, managerId, asNoTracking: true, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "MANAGER_NOT_FOUND", "Không tìm thấy tài khoản quản lý.");

        var distinctIds = (propertyIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
            throw new BusinessException(BusinessErrorType.Validation, "PROPERTY_IDS_REQUIRED", "Vui lòng chọn ít nhất một khu trọ để phân công.");

        var owned = await _store.GetOwnedPropertiesAsync(landlordId, distinctIds, ct);
        if (owned.Count != distinctIds.Count)
            throw new BusinessException(BusinessErrorType.NotFound, "PROPERTY_NOT_FOUND", "Một hoặc nhiều khu trọ không tồn tại hoặc không thuộc quyền sở hữu của bạn.");

        var existingActive = (await _store.GetActiveAssignedPropertyIdsAsync(manager.Id, ct)).ToHashSet();
        if (distinctIds.Any(id => existingActive.Contains(id)))
            throw new BusinessException(BusinessErrorType.Conflict, "ASSIGNMENT_ALREADY_EXISTS", "Quản lý đã được phân công một hoặc nhiều khu trọ này.");

        var now = DateTime.UtcNow;
        var assignments = distinctIds.Select(propertyId => new PropertyManagerAssignment
        {
            Id = Guid.NewGuid(),
            ManagerUserId = manager.Id,
            PropertyId = propertyId,
            AssignedByUserId = context.ActorUserId,
            AssignedAt = now
        }).ToList();

        await _store.AddAssignmentsAsync(assignments, ct);

        return await _store.GetManagerDetailsAsync(landlordId, manager.Id, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "MANAGER_NOT_FOUND", "Không tìm thấy tài khoản quản lý.");
    }

    public async Task RevokePropertyAsync(string managerId, Guid propertyId, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_current, _landlords, ct);
        var revoked = await _store.RevokeAssignmentAsync(context.OwnerLandlordUserId, managerId, propertyId, DateTime.UtcNow, ct);
        if (!revoked)
            throw new BusinessException(BusinessErrorType.NotFound, "ASSIGNMENT_NOT_FOUND", "Không tìm thấy phân công đang hiệu lực cho khu trọ này.");
    }

    public async Task<ManagerDto> SetLockStateAsync(string managerId, bool isActive, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_current, _landlords, ct);
        var manager = await _store.FindManagerAsync(context.OwnerLandlordUserId, managerId, asNoTracking: false, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "MANAGER_NOT_FOUND", "Không tìm thấy tài khoản quản lý.");

        manager.IsActive = isActive;
        await _store.SaveChangesAsync(ct);

        return await _store.GetManagerDetailsAsync(context.OwnerLandlordUserId, manager.Id, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "MANAGER_NOT_FOUND", "Không tìm thấy tài khoản quản lý.");
    }

    public async Task DeleteAsync(string managerId, CancellationToken ct = default)
    {
        var context = await OperatorAccessGuard.RequireLandlordAsync(_current, _landlords, ct);
        var deleted = await _store.DeleteOrDeactivateManagerAsync(context.OwnerLandlordUserId, managerId, DateTime.UtcNow, ct);
        if (!deleted)
            throw new BusinessException(BusinessErrorType.NotFound, "MANAGER_NOT_FOUND", "Không tìm thấy tài khoản quản lý.");
    }
}
