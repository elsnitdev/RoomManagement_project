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
[ApiController]
[Authorize(Roles = UserRoles.Landlord)]
[Route("api/landlord/tenants")]
public sealed class LandlordTenantsController : ControllerBase
{
    private readonly ITenantProfileService _tenantProfileService;
    private readonly ILogger<LandlordTenantsController> _logger;

    public LandlordTenantsController(
        ITenantProfileService tenantProfileService,
        ILogger<LandlordTenantsController> logger)
    {
        _tenantProfileService = tenantProfileService;
        _logger = logger;
    }

    /// <summary>
    /// API cho phép Chủ trọ tạo hồ sơ người thuê mới (chưa có tài khoản).
    /// </summary>
    /// <param name="request">Thông tin người thuê cần tạo.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    /// <returns>
    /// - 201 Created: Tạo hồ sơ thành công (bọc trong { data, message }).
    /// - 400 Bad Request: Dữ liệu nhập vào không hợp lệ.
    /// - 409 Conflict: Số CCCD đã được sử dụng bởi hồ sơ khác.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TenantProfileResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantProfileRequest request,
        CancellationToken cancellationToken)
    {
        var landlordId = GetCurrentUserId();
        if (string.IsNullOrEmpty(landlordId))
        {
            return Forbid();
        }

        _logger.LogInformation("Chủ trọ '{LandlordId}' gửi yêu cầu tạo hồ sơ người thuê '{FullName}'.",
            landlordId, request.FullName);

        try
        {
            var createdDto = await _tenantProfileService.CreateProfileAsync(
                landlordId, request.ToDto(), cancellationToken);

            var response = TenantProfileResponse.FromDto(createdDto);

            return StatusCode(StatusCodes.Status201Created, new
            {
                data = response,
                message = "Tạo hồ sơ người thuê thành công."
            });
        }
        catch (AppValidationException ex)
        {
            _logger.LogWarning(ex, "Lỗi kiểm tra tính hợp lệ khi tạo hồ sơ người thuê: {Message}", ex.Message);
            return BadRequest(ProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Dữ liệu không hợp lệ",
                ex.Message,
                code: ex.Code ?? "VALIDATION_FAILED"));
        }
        catch (AppConflictException ex)
        {
            _logger.LogWarning(ex, "Lỗi xung đột dữ liệu khi tạo hồ sơ người thuê: {Message}", ex.Message);
            return Conflict(ProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Xung đột dữ liệu",
                ex.Message,
                code: ex.Code ?? "CITIZEN_ID_CONFLICT"));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Vi phạm quy tắc nghiệp vụ khi tạo hồ sơ người thuê: {Message}", ex.Message);
            return UnprocessableEntity(ProblemDetailsHelper.Create(
                StatusCodes.Status422UnprocessableEntity,
                "Vi phạm quy tắc nghiệp vụ",
                ex.Message,
                code: ex.Code ?? "BUSINESS_RULE_VIOLATION"));
        }
        catch (Exception ex)
        {
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
    /// Chỉ xem được hồ sơ do chính mình tạo hoặc người thuê có hợp đồng tại khu trọ do mình sở hữu.
    /// </summary>
    /// <param name="id">Mã định danh của hồ sơ người thuê.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    /// <returns>
    /// - 200 OK: Trả về thông tin chi tiết bọc trong { data }.
    /// - 404 Not Found: Không tìm thấy hồ sơ hoặc chủ trọ không có quyền xem.
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TenantProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var landlordId = GetCurrentUserId();
        if (string.IsNullOrEmpty(landlordId))
        {
            return Forbid();
        }

        try
        {
            var dto = await _tenantProfileService.GetProfileByIdAsync(landlordId, id, cancellationToken);
            var response = TenantProfileResponse.FromDto(dto);

            return Ok(new
            {
                data = response
            });
        }
        catch (AppNotFoundException ex)
        {
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
    /// </summary>
    /// <param name="keyword">Từ khóa tìm kiếm theo họ tên, số điện thoại hoặc CCCD.</param>
    /// <param name="page">Trang cần lấy (mặc định 1).</param>
    /// <param name="pageSize">Số lượng hồ sơ trên một trang (mặc định 10).</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    /// <returns>
    /// - 200 OK: Danh sách hồ sơ bọc trong envelope chuẩn { data, meta }.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiPagedResponse<TenantProfileResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
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
            var query = new TenantProfileQueryDto
            {
                Keyword = keyword,
                PageIndex = page,
                PageSize = pageSize
            };

            var (items, totalCount) = await _tenantProfileService.GetProfilesAsync(
                landlordId, query, cancellationToken);

            var responses = items.Select(TenantProfileResponse.FromDto).ToList();

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
    /// </summary>
    /// <param name="id">Mã định danh hồ sơ người thuê.</param>
    /// <param name="request">Dữ liệu cập nhật mới.</param>
    /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ.</param>
    /// <returns>
    /// - 200 OK: Cập nhật thành công (bọc trong { data, message }).
    /// - 400 Bad Request: Dữ liệu không hợp lệ.
    /// - 404 Not Found: Không tìm thấy hồ sơ hoặc không có quyền thao tác.
    /// - 409 Conflict: Số CCCD trùng với hồ sơ khác.
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TenantProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantProfileRequest request,
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
            var updatedDto = await _tenantProfileService.UpdateProfileAsync(
                landlordId, id, request.ToDto(), cancellationToken);

            var response = TenantProfileResponse.FromDto(updatedDto);

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

    private string? GetCurrentUserId()
    {
        return User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
