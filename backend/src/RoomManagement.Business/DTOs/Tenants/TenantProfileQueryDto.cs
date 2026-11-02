namespace RoomManagement.Business.DTOs.Tenants;

/// <summary>
/// DTO chứa tham số tìm kiếm và phân trang danh sách hồ sơ người thuê của Chủ trọ.
/// </summary>
public sealed class TenantProfileQueryDto
{
    /// <summary>Từ khóa tìm kiếm (theo họ tên, số điện thoại, CCCD...).</summary>
    public string? Keyword { get; set; }

    /// <summary>Chỉ số trang (bắt đầu từ 1, mặc định 1).</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>Số lượng bản ghi trên một trang (mặc định 10).</summary>
    public int PageSize { get; set; } = 10;
}
