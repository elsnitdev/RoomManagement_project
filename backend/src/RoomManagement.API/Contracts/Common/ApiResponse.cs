namespace RoomManagement.API.Contracts.Common;

/// <summary>
/// Chuẩn Envelope phản hồi cho kết quả đơn lẻ tuân thủ nghiêm ngặt quy định tại ai-feature-workflow-guide.md:
/// "Thành công đơn lẻ dùng { data }".
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của payload phản hồi.</typeparam>
public sealed class ApiResponse<T>
{
    /// <summary>
    /// Payload dữ liệu nghiệp vụ chính.
    /// </summary>
    public T Data { get; init; } = default!;

    /// <summary>
    /// Thông điệp phụ trợ tùy chọn (tương thích ngược với client đọc thông điệp).
    /// </summary>
    public string? Message { get; init; }

    public ApiResponse() { }

    public ApiResponse(T data, string? message = null)
    {
        Data = data;
        Message = message;
    }
}

/// <summary>
/// Chuẩn Envelope phản hồi cho danh sách / dữ liệu phân trang tuân thủ quy định tại ai-feature-workflow-guide.md:
/// "danh sách phân trang dùng { data, meta }".
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của từng phần tử trong danh sách.</typeparam>
public sealed class ApiPagedResponse<T>
{
    /// <summary>
    /// Danh sách dữ liệu các phần tử.
    /// </summary>
    public IEnumerable<T> Data { get; init; } = Enumerable.Empty<T>();

    /// <summary>
    /// Siêu dữ liệu phân trang (tổng số lượng, trang hiện tại, kích thước trang...).
    /// </summary>
    public PaginationMeta Meta { get; init; } = new();

    /// <summary>
    /// Tổng số lượng phần tử (hỗ trợ tương thích ngược với các client đọc thẳng root.total).
    /// </summary>
    public int Total => Meta.Total;

    public ApiPagedResponse() { }

    public ApiPagedResponse(IEnumerable<T> data, int total, int? page = null, int? pageSize = null)
    {
        Data = data;
        Meta = new PaginationMeta
        {
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}

/// <summary>
/// Cấu trúc siêu dữ liệu phân trang (Pagination Metadata).
/// </summary>
public sealed class PaginationMeta
{
    /// <summary>
    /// Tổng số bản ghi tìm thấy thỏa mãn điều kiện lọc.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Trang hiện tại (1-indexed), null nếu danh sách không phân trang theo số trang.
    /// </summary>
    public int? Page { get; init; }

    /// <summary>
    /// Số lượng bản ghi tối đa trên một trang.
    /// </summary>
    public int? PageSize { get; init; }
}
