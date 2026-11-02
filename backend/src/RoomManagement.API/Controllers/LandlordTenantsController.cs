using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Common;
using RoomManagement.API.Contracts.Common;
using RoomManagement.API.Contracts.Tenants;
using RoomManagement.Business.DTOs.Tenants;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Interfaces;
using RoomManagement.Domain;

namespace RoomManagement.API.Controllers;

/// <summary>
/// Controller phục vụ Chủ trọ (Landlord) quản lý danh sách và thông tin hồ sơ Người thuê (TenantProfile).
/// Hỗ trợ tạo và lưu thông tin người thuê độc lập ngay cả khi người đó chưa có tài khoản đăng nhập.
/// Tuân thủ quy chuẩn Clean Architecture và ai-feature-workflow-guide.md:
/// - Phản hồi đơn lẻ bọc trong { data } hoặc { data, message }.
/// - Phản hồi phân trang bọc trong { data, meta }.
/// - Phản hồi lỗi chuẩn hóa ProblemDetails (400, 404, 409, 422, 500).
/// </summary>
[ApiController] // Tự động kiểm tra ModelState và hỗ trợ binding dữ liệu JSON từ body/query
[Authorize(Roles = UserRoles.Landlord)] // Yêu cầu người gọi phải đăng nhập (có JWT Token hợp lệ) VÀ có vai trò là Landlord
[Route("api/landlord/tenants")] // Đường dẫn gốc (Base URL) cho toàn bộ các API trong Controller này
public sealed class LandlordTenantsController : ControllerBase
{
    // Khai báo interface của tầng Business và công cụ ghi log
    private readonly ITenantProfileService _tenantProfileService;
    private readonly ILogger<LandlordTenantsController> _logger;

    // Constructor nhận các dịch vụ thông qua cơ chế Dependency Injection (DI) của ASP.NET Core
    public LandlordTenantsController(
        ITenantProfileService tenantProfileService,
        ILogger<LandlordTenantsController> logger)
    {
        _tenantProfileService = tenantProfileService;
        _logger = logger;
    }

    /// <summary>
    /// API cho phép Chủ trọ tạo hồ sơ người thuê mới (chưa có tài khoản).
    /// Phương thức: POST /api/landlord/tenants
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TenantProfileResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantProfileRequest request, // [FromBody]: Đọc dữ liệu JSON từ phần thân (body) của HTTP Request
        CancellationToken cancellationToken) // Token giúp hủy request nếu phía trình duyệt ngắt kết nối giữa chừng
    {
        // Lấy ID của chủ trọ đang đăng nhập từ trong JWT Token
        var landlordId = GetCurrentUserId();
        if (string.IsNullOrEmpty(landlordId))
        {
            // Nếu không xác định được danh tính chủ trọ -> trả về 403 Forbidden (Từ chối truy cập)
            return Forbid();
        }

        _logger.LogInformation("Chủ trọ '{LandlordId}' gửi yêu cầu tạo hồ sơ người thuê '{FullName}'.",
            landlordId, request.FullName);

        try
        {
            // Gọi tầng Business để xử lý nghiệp vụ tạo hồ sơ
            var createdDto = await _tenantProfileService.CreateProfileAsync(
                landlordId, request.ToDto(), cancellationToken);

            // Chuyển đổi kết quả từ Business DTO sang Response Contract để trả về cho Frontend
            var response = TenantProfileResponse.FromDto(createdDto);

            // Trả về HTTP 201 Created cùng cấu trúc JSON: { data: ..., message: "..." }
            return StatusCode(StatusCodes.Status201Created, new
            {
                data = response,
                message = "Tạo hồ sơ người thuê thành công."
            });
        }
        catch (AppValidationException ex)
        {
            // Bắt lỗi dữ liệu đầu vào không hợp lệ -> trả về HTTP 400 Bad Request
            _logger.LogWarning(ex, "Lỗi kiểm tra tính hợp lệ khi tạo hồ sơ người thuê: {Message}", ex.Message);
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppConflictException ex)
        {
            // Bắt lỗi trùng số CCCD -> trả về HTTP 409 Conflict
            _logger.LogWarning(ex, "Lỗi xung đột dữ liệu khi tạo hồ sơ người thuê: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "CITIZEN_ID_CONFLICT"));
        }
        catch (BusinessRuleException ex)
        {
            // Bắt lỗi vi phạm quy tắc nghiệp vụ -> trả về HTTP 422 Unprocessable Entity
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ khi tạo hồ sơ người thuê: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (Exception ex)
        {
            // Bắt mọi lỗi hệ thống ngoài dự kiến -> trả về HTTP 500 Internal Server Error
            _logger.LogError(ex, "Lỗi máy chủ khi tạo hồ sơ người thuê.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình tạo hồ sơ người thuê. Vui lòng thử lại sau.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }

    /// <summary>
    /// API xem chi tiết thông tin hồ sơ người thuê.
    /// Phương thức: GET /api/landlord/tenants/{id}
    /// </summary>
    [HttpGet("{id:guid}")] // Ràng buộc tham số {id} trên URL phải có định dạng kiểu Guid hợp lệ
    [ProducesResponseType(typeof(ApiResponse<TenantProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id, // [FromRoute]: Lấy giá trị {id} trực tiếp từ đường dẫn URL
        CancellationToken cancellationToken)
    {
        var landlordId = GetCurrentUserId();
        if (string.IsNullOrEmpty(landlordId))
        {
            return Forbid();
        }

        try
        {
            // Gọi tầng Business lấy chi tiết hồ sơ (đã bao gồm bước kiểm tra quyền truy cập của chủ trọ)
            var dto = await _tenantProfileService.GetProfileByIdAsync(landlordId, id, cancellationToken);
            var response = TenantProfileResponse.FromDto(dto);

            // Trả về HTTP 200 OK bọc trong đối tượng { data }
            return Ok(new
            {
                data = response
            });
        }
        catch (AppNotFoundException ex)
        {
            // Nếu không tìm thấy hoặc chủ trọ không có quyền xem -> trả về HTTP 404 Not Found
            _logger.LogWarning(ex, "Không tìm thấy hồ sơ người thuê: {Message}", ex.Message);
            return NotFound(ProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "Không tìm thấy tài nguyên",
                ex.Message,
                code: ex.Code ?? "TENANT_PROFILE_NOT_FOUND"));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ khi xem hồ sơ người thuê: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi máy chủ khi xem chi tiết hồ sơ người thuê ID: {ProfileId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình lấy thông tin hồ sơ người thuê.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }

    /// <summary>
    /// API xem danh sách hồ sơ người thuê thuộc quyền quản lý của Chủ trọ (có phân trang và tìm kiếm).
    /// Phương thức: GET /api/landlord/tenants?keyword=...&page=1&pageSize=10
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiPagedResponse<TenantProfileResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword, // [FromQuery]: Đọc tham số trên thanh địa chỉ sau dấu '?' (Query String)
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var landlordId = GetCurrentUserId();
        if (string.IsNullOrEmpty(landlordId))
        {
            return Forbid();
        }

        try
        {
            // Đóng gói các tham số tìm kiếm & phân trang vào DTO để chuyển xuống tầng Business
            var query = new TenantProfileQueryDto
            {
                Keyword = keyword,
                PageIndex = page,
                PageSize = pageSize
            };

            // Lấy danh sách bản ghi của trang hiện tại và tổng số lượng bản ghi thỏa mãn
            var (items, totalCount) = await _tenantProfileService.GetProfilesAsync(
                landlordId, query, cancellationToken);

            // Chuyển đổi từng phần tử sang TenantProfileResponse
            var responses = items.Select(TenantProfileResponse.FromDto).ToList();

            // Trả về HTTP 200 OK kèm cấu trúc phân trang chuẩn { data: [...], meta: { total, page, pageSize } }
            return Ok(new ApiPagedResponse<TenantProfileResponse>(responses, totalCount, page, pageSize));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ khi lấy danh sách hồ sơ: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (AppValidationException ex)
        {
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi máy chủ khi lấy danh sách hồ sơ người thuê.");
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình truy vấn danh sách hồ sơ người thuê.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }

    /// <summary>
    /// API cập nhật thông tin hồ sơ người thuê.
    /// Phương thức: PUT /api/landlord/tenants/{id}
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TenantProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id, // Mã hồ sơ cần sửa lấy từ URL
        [FromBody] UpdateTenantProfileRequest request, // Dữ liệu mới lấy từ JSON body
        CancellationToken cancellationToken)
    {
        var landlordId = GetCurrentUserId();
        if (string.IsNullOrEmpty(landlordId))
        {
            return Forbid();
        }

        _logger.LogInformation("Chủ trọ '{LandlordId}' cập nhật hồ sơ người thuê ID: {ProfileId}.",
            landlordId, id);

        try
        {
            // Gọi tầng Business kiểm tra quyền, kiểm tra dữ liệu & CCCD trùng, rồi lưu cập nhật
            var updatedDto = await _tenantProfileService.UpdateProfileAsync(
                landlordId, id, request.ToDto(), cancellationToken);

            var response = TenantProfileResponse.FromDto(updatedDto);

            // Trả về HTTP 200 OK kèm dữ liệu mới sau khi sửa
            return Ok(new
            {
                data = response,
                message = "Cập nhật hồ sơ người thuê thành công."
            });
        }
        catch (AppValidationException ex)
        {
            _logger.LogWarning(ex, "Lỗi kiểm tra tính hợp lệ khi cập nhật hồ sơ: {Message}", ex.Message);
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppNotFoundException ex)
        {
            _logger.LogWarning(ex, "Không tìm thấy hồ sơ cần cập nhật: {Message}", ex.Message);
            return NotFound(ProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "Không tìm thấy tài nguyên",
                ex.Message,
                code: ex.Code ?? "TENANT_PROFILE_NOT_FOUND"));
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Xung đột CCCD khi cập nhật hồ sơ: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "CITIZEN_ID_CONFLICT"));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ khi cập nhật hồ sơ: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi máy chủ khi cập nhật hồ sơ người thuê ID: {ProfileId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ProblemDetailsHelper.Create(
                StatusCodes.Status500InternalServerError,
                "Lỗi máy chủ nội bộ",
                "Hệ thống gặp sự cố trong quá trình cập nhật hồ sơ người thuê.",
                code: "INTERNAL_SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Hàm phụ trợ giúp trích xuất User ID của người dùng đang đăng nhập từ JWT Claims.
    /// </summary>
    private string? GetCurrentUserId()
    {
        // Tìm claim "sub" (Subject - chuẩn JWT) hoặc ClaimTypes.NameIdentifier trong thông tin xác thực User
        return User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
