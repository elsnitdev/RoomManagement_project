# Chương 1 — Hoàn thiện bộ file Contract đang để trống

**Đích của chương:** `POST /api/properties/{propertyId}/contracts` tạo hợp đồng `Draft`; xem danh sách/chi tiết, gắn người thuê, rồi `activate`. Chưa tạo hóa đơn ở chương này. Sơ đồ gọi: `ContractsController → ContractService → IContractStore → ContractStore → DbContext`.

Các entity và cấu hình bảng đã có: `backend/src/RoomManagement.Domain/Entities/Contract.cs`, `ContractTenant.cs`, `TenantProfile.cs` và `backend/src/RoomManagement.Data/Configurations/ContractConfiguration.cs`. Bảng `Contracts` đã chặn hai hợp đồng `Active` cùng phòng bằng unique filtered index. **Không sửa các entity trước khi hoàn thành lát cắt tối thiểu.**

## 1. `Business/DTOs/ContractModels.cs` — dữ liệu đi vào service

**Đường dẫn đầy đủ trong repo:** `backend/src/RoomManagement.Business/DTOs/ContractModels.cs`. File hiện rỗng. Ghi các record dưới đây: chúng không chứa HTTP attribute, chỉ là dữ liệu nghiệp vụ. `PageResult<T>` đã tồn tại trong `PropertyRoomModels.cs` nên không khai báo lại.

```csharp
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.DTOs;

public sealed record CreateContractInput(
    Guid RoomId, string ContractCode, DateOnly StartDate, DateOnly? EndDate,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay, string? Note);

public sealed record AddContractTenantInput(Guid TenantId, DateOnly JoinDate);

public sealed record ContractFilter(
    int Page, int PageSize, Guid? RoomId, ContractStatus? Status);
```

**Tại sao:** controller chỉ đổi JSON thành các record này; `ContractService` nhận input không phụ thuộc ASP.NET. `ContractFilter` giúp store phân trang ở SQL, không kéo toàn bộ hợp đồng về RAM.

## 2. `Business/Interfaces/IContractStore.cs` — danh sách thao tác Data phải hỗ trợ

**File hiện chỉ có interface rỗng.** Thay phần thân bằng các chữ ký sau. Interface đặt ở Business để `ContractService` không cần biết EF Core.

```csharp
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Interfaces;

public interface IContractStore
{
    Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct);
    Task<Contract?> GetOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct);
    Task<(IReadOnlyList<Contract> Items, long Total)> ListOwnedAsync(
        Guid propertyId, string ownerId, Guid? roomId, ContractStatus? status,
        int skip, int take, CancellationToken ct);
    Task<bool> TenantProfileExistsAsync(Guid tenantId, CancellationToken ct);
    Task<bool> HasCurrentTenantAsync(Guid contractId, CancellationToken ct);
    Task<bool> TenantAlreadyCurrentAsync(Guid contractId, Guid tenantId, CancellationToken ct);
    Task AddAsync(Contract contract, CancellationToken ct);
    Task AddTenantAsync(ContractTenant tenant, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
```

**Trách nhiệm từng nhóm:** `RoomOwnedAsync` chặn tạo hợp đồng cho phòng người khác; `GetOwnedAsync/ListOwnedAsync` chặn đọc chéo; nhóm Tenant kiểm tra hồ sơ và người ở; `Add/Save` ghi vào DB. Service không tự dùng `DbContext`.

## 3. `Data/Stores/ContractStore.cs` — code cụ thể cần ghi

**Đường dẫn:** `backend/src/RoomManagement.Data/Stores/ContractStore.cs`. File đang có `namespace RoomManagement.Data; public sealed class ContractStore : IContractStore { }` nhưng chưa có constructor hoặc method. Giữ namespace này để DI hiện tại nhận đúng kiểu. Nội dung tối thiểu:

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class ContractStore : IContractStore
{
    private readonly RoomManagementDbContext _db;
    public ContractStore(RoomManagementDbContext db) => _db = db;

    // Mọi query Contract của landlord đều đi qua phép nối này.
    private IQueryable<Contract> Owned(Guid propertyId, string ownerId) =>
        from contract in _db.Contracts
        join room in _db.Rooms on contract.RoomId equals room.Id
        join property in _db.Properties on room.PropertyId equals property.Id
        where property.Id == propertyId && property.OwnerUserId == ownerId
        select contract;

    public Task<bool> RoomOwnedAsync(Guid propertyId, Guid roomId, string ownerId, CancellationToken ct) =>
        _db.Rooms.AnyAsync(room => room.Id == roomId && room.PropertyId == propertyId &&
            _db.Properties.Any(property => property.Id == propertyId &&
                property.OwnerUserId == ownerId && property.IsActive), ct);

    public Task<Contract?> GetOwnedAsync(Guid propertyId, Guid contractId, string ownerId, CancellationToken ct) =>
        Owned(propertyId, ownerId).SingleOrDefaultAsync(c => c.Id == contractId, ct);

    public async Task<(IReadOnlyList<Contract> Items, long Total)> ListOwnedAsync(
        Guid propertyId, string ownerId, Guid? roomId, ContractStatus? status,
        int skip, int take, CancellationToken ct)
    {
        var query = Owned(propertyId, ownerId).AsNoTracking();
        if (roomId.HasValue) query = query.Where(c => c.RoomId == roomId.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<bool> TenantProfileExistsAsync(Guid tenantId, CancellationToken ct) =>
        _db.TenantProfiles.AnyAsync(t => t.Id == tenantId, ct);

    public Task<bool> HasCurrentTenantAsync(Guid contractId, CancellationToken ct) =>
        _db.ContractTenants.AnyAsync(t => t.ContractId == contractId && t.LeaveDate == null, ct);

    public Task<bool> TenantAlreadyCurrentAsync(Guid contractId, Guid tenantId, CancellationToken ct) =>
        _db.ContractTenants.AnyAsync(t => t.ContractId == contractId &&
            t.TenantId == tenantId && t.LeaveDate == null, ct);

    public async Task AddAsync(Contract contract, CancellationToken ct)
    {
        _db.Contracts.Add(contract);
        await SaveAsync(ct);
    }

    public async Task AddTenantAsync(ContractTenant tenant, CancellationToken ct)
    {
        _db.ContractTenants.Add(tenant);
        await SaveAsync(ct);
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict,
                "CONTRACT_CONFLICT", "Mã hợp đồng, phòng đang thuê hoặc liên kết người thuê bị trùng.", ex);
        }
    }
}
```

**Giải thích quan trọng:** `Owned` là chốt quyền ở tầng dữ liệu. Nếu chỉ `[Authorize(Roles = Landlord)]` ở controller, landlord A vẫn có thể đoán ID hợp đồng của landlord B. `GetOwnedAsync` không dùng `AsNoTracking()` vì service sẽ lấy entity này để đổi trạng thái và gọi `SaveAsync`. `ListOwnedAsync` chỉ đọc nên dùng `AsNoTracking()`. Unique index là chốt cuối khi hai request kích hoạt hợp đồng cùng phòng đến đồng thời.

## 4. `Business/Services/ContractService.cs` — kiểm tra và điều phối

File hiện rỗng. Tạo class nhận `ICurrentLandlord`, `ILandlordStore`, `IContractStore`. Service không viết LINQ EF. Đây là **một lát cắt đầy đủ** để tạo, đọc, gắn tenant và kích hoạt:

```csharp
using System.Text.RegularExpressions;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public sealed class ContractService
{
    private readonly ICurrentLandlord _current;
    private readonly ILandlordStore _landlords;
    private readonly IContractStore _store;

    public ContractService(ICurrentLandlord current, ILandlordStore landlords, IContractStore store)
    {
        _current = current; _landlords = landlords; _store = store;
    }

    private async Task<string> OwnerAsync(CancellationToken ct)
    {
        var owner = _current.GetUserId();
        if (string.IsNullOrWhiteSpace(owner) || !await _landlords.IsActiveLandlordAsync(owner, ct))
            throw new BusinessException(BusinessErrorType.InvalidOperation,
                "LANDLORD_UNAVAILABLE", "Chủ trọ không khả dụng.");
        return owner;
    }

    public async Task<Contract> CreateAsync(Guid propertyId, CreateContractInput input, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        if (!await _store.RoomOwnedAsync(propertyId, input.RoomId, owner, ct))
            throw new BusinessException(BusinessErrorType.NotFound,
                "ROOM_NOT_FOUND", "Không tìm thấy phòng trong khu trọ.");
        var code = input.ContractCode?.Trim().ToUpperInvariant() ?? "";
        if (!Regex.IsMatch(code, "^[A-Z0-9._-]{1,50}$") ||
            (input.EndDate is { } end && end < input.StartDate) ||
            input.MonthlyRent < 0 || input.DepositAmount < 0 ||
            input.PaymentDueDay is < 1 or > 31 || input.Note is { Length: > 1000 })
            throw new BusinessException(BusinessErrorType.Validation,
                "CONTRACT_INVALID", "Thông tin hợp đồng không hợp lệ.");
        var contract = new Contract
        {
            Id = Guid.NewGuid(), ContractCode = code, RoomId = input.RoomId,
            StartDate = input.StartDate, EndDate = input.EndDate,
            MonthlyRent = input.MonthlyRent, DepositAmount = input.DepositAmount,
            PaymentDueDay = input.PaymentDueDay, Note = input.Note?.Trim(),
            Status = ContractStatus.Draft, CreatedAt = DateTime.UtcNow
        };
        await _store.AddAsync(contract, ct);
        return contract;
    }

    public async Task<Contract> GetAsync(Guid propertyId, Guid contractId, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        return await _store.GetOwnedAsync(propertyId, contractId, owner, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound,
                "CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng.");
    }

    public async Task<PageResult<Contract>> ListAsync(Guid propertyId, ContractFilter filter, CancellationToken ct)
    {
        var owner = await OwnerAsync(ct);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100)
            throw new BusinessException(BusinessErrorType.Validation,
                "PAGE_INVALID", "Trang hoặc số bản ghi không hợp lệ.");
        var (items, total) = await _store.ListOwnedAsync(propertyId, owner,
            filter.RoomId, filter.Status, (filter.Page - 1) * filter.PageSize, filter.PageSize, ct);
        return new PageResult<Contract>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<ContractTenant> AddTenantAsync(
        Guid propertyId, Guid contractId, AddContractTenantInput input, CancellationToken ct)
    {
        var contract = await GetAsync(propertyId, contractId, ct);
        if (contract.Status is not (ContractStatus.Draft or ContractStatus.Active) ||
            input.JoinDate < contract.StartDate ||
            (contract.EndDate is { } end && input.JoinDate > end) ||
            !await _store.TenantProfileExistsAsync(input.TenantId, ct) ||
            await _store.TenantAlreadyCurrentAsync(contractId, input.TenantId, ct))
            throw new BusinessException(BusinessErrorType.Validation,
                "CONTRACT_TENANT_INVALID", "Người thuê hoặc ngày tham gia không hợp lệ.");
        var row = new ContractTenant
        {
            Id = Guid.NewGuid(), ContractId = contractId, TenantId = input.TenantId,
            JoinDate = input.JoinDate, CreatedAt = DateTime.UtcNow
        };
        await _store.AddTenantAsync(row, ct);
        return row;
    }

    public async Task<Contract> ActivateAsync(Guid propertyId, Guid contractId, CancellationToken ct)
    {
        var contract = await GetAsync(propertyId, contractId, ct);
        if (contract.Status != ContractStatus.Draft ||
            !await _store.HasCurrentTenantAsync(contractId, ct))
            throw new BusinessException(BusinessErrorType.InvalidOperation,
                "CONTRACT_CANNOT_ACTIVATE", "Hợp đồng chưa sẵn sàng kích hoạt.");
        contract.Status = ContractStatus.Active;
        contract.UpdatedAt = DateTime.UtcNow;
        await _store.SaveAsync(ct);
        return contract;
    }
}
```

**Chỗ cần nghĩ thêm khi hoàn thiện production:** `TenantProfile` hiện chưa gắn Property, nên `TenantProfileExistsAsync` chỉ xác nhận hồ sơ tồn tại, chưa chứng minh chủ trọ có quyền thêm người thuê đó. Nếu người thuê có thể được dùng chung toàn hệ thống, cần chính sách mời/xác nhận; không cho phép chủ trọ tự gắn một hồ sơ bất kỳ chỉ vì biết GUID. Luồng này phải được chốt và test trước khi mở endpoint cho người dùng thật. Ngoài ra, nếu bạn yêu cầu activation nguyên tử với việc thêm tenant, gom vào transaction store.

## 5. `API/Contracts/Contracts/ContractRequests.cs` — JSON và validation

File hiện rỗng. Request phản ánh dữ liệu HTTP và đổi sang input ở mục 1. Dùng `JsonRequired` để client không vô tình bỏ trường bắt buộc; service vẫn kiểm tra lại giá trị.

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RoomManagement.Business.DTOs;

namespace RoomManagement.API.Contracts.Contracts;

public sealed record CreateContractRequest(
    [property: JsonRequired] Guid RoomId,
    [property: JsonRequired, Required, StringLength(50)] string ContractCode,
    [property: JsonRequired] DateOnly StartDate,
    DateOnly? EndDate,
    [property: JsonRequired] decimal MonthlyRent,
    [property: JsonRequired] decimal DepositAmount,
    byte? PaymentDueDay,
    [StringLength(1000)] string? Note)
{
    public CreateContractInput ToInput() => new(RoomId, ContractCode, StartDate,
        EndDate, MonthlyRent, DepositAmount, PaymentDueDay, Note);
}

public sealed record AddContractTenantRequest(
    [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] DateOnly JoinDate)
{
    public AddContractTenantInput ToInput() => new(TenantId, JoinDate);
}
```

**Chú ý:** `Guid.Empty` và `DateOnly.MinValue` vẫn có thể được gửi; service nên kiểm tra rõ hai giá trị này nếu muốn trả lỗi đẹp trước FK/check của SQL Server.

## 6. `API/Contracts/Contracts/ContractResponse.cs` — hình dạng JSON trả về

File hiện rỗng. Chỉ đưa field cần cho UI; không trả entity EF trực tiếp. Đặt code sau:

```csharp
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Contracts.Contracts;

public sealed record ContractResponse(
    Guid Id, string ContractCode, Guid RoomId, DateOnly StartDate, DateOnly? EndDate,
    decimal MonthlyRent, decimal DepositAmount, byte? PaymentDueDay,
    ContractStatus Status, string? Note)
{
    public static ContractResponse From(Contract c) => new(c.Id, c.ContractCode, c.RoomId,
        c.StartDate, c.EndDate, c.MonthlyRent, c.DepositAmount,
        c.PaymentDueDay, c.Status, c.Note);
}

public sealed record ContractTenantResponse(Guid Id, Guid ContractId, Guid TenantId, DateOnly JoinDate)
{
    public static ContractTenantResponse From(ContractTenant t) =>
        new(t.Id, t.ContractId, t.TenantId, t.JoinDate);
}
```

## 7. `API/Controllers/ContractsController.cs` — route cụ thể

File hiện rỗng. Controller **không** chứa tính tiền/EF; chỉ nhận request, gọi service và trả status. Mẫu có đủ route của chương:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Contracts;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Services;
using RoomManagement.Domain;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/properties/{propertyId:guid}/contracts")]
public sealed class ContractsController : ControllerBase
{
    private readonly ContractService _service;
    public ContractsController(ContractService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create(Guid propertyId,
        [FromBody] CreateContractRequest request, CancellationToken ct)
    {
        var result = ContractResponse.From(await _service.CreateAsync(propertyId, request.ToInput(), ct));
        return CreatedAtAction(nameof(Get), new { propertyId, contractId = result.Id },
            new ApiResponse<ContractResponse>(result));
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid propertyId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? roomId = null, [FromQuery] ContractStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(propertyId,
            new ContractFilter(page, pageSize, roomId, status), ct);
        return Ok(new PageResponse<ContractResponse>(
            result.Items.Select(ContractResponse.From).ToList(),
            new PageMeta(page, pageSize, result.TotalItems,
                (int)Math.Ceiling((double)result.TotalItems / pageSize))));
    }

    [HttpGet("{contractId:guid}")]
    public async Task<IActionResult> Get(Guid propertyId, Guid contractId, CancellationToken ct) =>
        Ok(new ApiResponse<ContractResponse>(
            ContractResponse.From(await _service.GetAsync(propertyId, contractId, ct))));

    [HttpPost("{contractId:guid}/tenants")]
    public async Task<IActionResult> AddTenant(Guid propertyId, Guid contractId,
        [FromBody] AddContractTenantRequest request, CancellationToken ct) =>
        StatusCode(201, new ApiResponse<ContractTenantResponse>(ContractTenantResponse.From(
            await _service.AddTenantAsync(propertyId, contractId, request.ToInput(), ct))));

    [HttpPost("{contractId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid propertyId, Guid contractId, CancellationToken ct) =>
        Ok(new ApiResponse<ContractResponse>(ContractResponse.From(
            await _service.ActivateAsync(propertyId, contractId, ct))));
}
```

## 8. DI và checkpoint

**Hai file đã được bạn sửa:** `backend/src/RoomManagement.Data/ServiceCollectionExtensions.cs` đã có `AddScoped<IContractStore, ContractStore>()`; `backend/src/RoomManagement.API/Program.cs` đã có `AddScoped<ContractService>()`. Giữ hai dòng này và kiểm tra `using` trỏ đúng namespace `RoomManagement.Data` của `ContractStore`. Repo chưa có API tạo `TenantProfile`: để thử checkpoint, tạo hồ sơ tenant bằng fixture test SQL; trước khi dùng thật, triển khai route tạo hồ sơ tenant và chính sách ai được gắn hồ sơ đó vào hợp đồng.

Chạy `dotnet build backend/RoomManagement.sln`. Nếu build đạt, dùng một chủ trọ đã đăng nhập để tạo hợp đồng `Draft`, gắn một `TenantProfile`, kích hoạt, rồi gọi `GET` và `LIST`. Gọi cùng `contractId` bằng chủ trọ khác phải ra lỗi tài nguyên không tìm thấy. Hai request kích hoạt hợp đồng `Active` khác nhau cho cùng phòng: tối đa một request được chấp nhận; request kia phải là `409` từ unique index.

**Chỉ khi checkpoint này đạt mới sang [Chương 2](02-billing-inputs.md).**
