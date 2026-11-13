namespace RoomManagement.Business.DTOs.Tenants;

public sealed record LookupTenantAccountInput(Guid? PropertyId, string PhoneNumber, string CitizenId);

public sealed record TenantLookupResultDto(
    string UserId,
    Guid TenantProfileId,
    string FullName,
    string MaskedPhoneNumber,
    string MaskedCitizenId,
    string IdentityVerificationStatus);
