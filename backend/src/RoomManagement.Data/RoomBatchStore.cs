using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Data;

public sealed class RoomBatchStore : IRoomBatchStore
{
    private readonly RoomManagementDbContext _db;
    public RoomBatchStore(RoomManagementDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomRow>> ListRowsAsync(Guid propertyId, CancellationToken ct) =>
        await _db.RoomRows.AsNoTracking().Where(x => x.PropertyId == propertyId)
            .OrderBy(x => x.Code).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<RoomRow> AddRowAsync(Guid propertyId, string code, string name, CancellationToken ct)
    {
        var row = new RoomRow { Id = Guid.NewGuid(), PropertyId = propertyId, Code = code, Name = name };
        _db.RoomRows.Add(row);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_ROW_CODE_EXISTS", "Mã dãy đã tồn tại trong khu trọ.", ex);
        }
        return row;
    }

    /// <inheritdoc />
    public async Task<RoomRow> UpdateRowAsync(Guid propertyId, Guid rowId, string name, bool isActive, CancellationToken ct)
    {
        var row = await _db.RoomRows.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == rowId, ct)
            ?? throw new BusinessException(BusinessErrorType.NotFound, "ROOM_ROW_NOT_FOUND", "Không tìm thấy dãy trọ.");
        row.Name = name;
        row.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>Tạo lỗi khi dãy phòng không hợp lệ hoặc đã ngừng hoạt động.</summary>
    private static BusinessException InvalidRow() =>
        new(BusinessErrorType.Validation, "ROOM_ROW_INVALID", "Dãy trọ không thuộc khu trọ hoặc đã ngừng hoạt động.");

    /// <summary>Tính số phòng dự kiến và tạo dấu xác nhận bản xem trước.</summary>
    private async Task<RoomBatchPreviewResult> BuildPreviewAsync(Guid propertyId, RoomBatchPlan plan, CancellationToken ct)
    {
        var rowIds = plan.Groups.Where(g => g.RowId.HasValue).Select(g => g.RowId!.Value).Distinct().ToArray();
        var rows = await _db.RoomRows.AsNoTracking()
            .Where(r => r.PropertyId == propertyId && rowIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);
        if (rows.Count != rowIds.Length || rows.Values.Any(r => !r.IsActive)) throw InvalidRow();

        var counters = await _db.RoomNumberCounters.AsNoTracking()
            .Where(c => c.PropertyId == propertyId).ToDictionaryAsync(c => c.ScopeKey, ct);
        var occupied = (await _db.Rooms.AsNoTracking().Where(r => r.PropertyId == propertyId)
                .Select(r => r.RoomNumber).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overrides = plan.Overrides.ToDictionary(o => (o.GroupKey, o.Ordinal));
        var result = new List<RoomBatchPreviewItem>();
        foreach (var group in plan.Groups)
        {
            var code = group.RowId.HasValue ? rows[group.RowId.Value].Code : null;
            var key = RoomNumbering.Scope(group.RowId, group.Floor);
            var sequence = counters.TryGetValue(key, out var counter) ? counter.LastSequence : 0;
            for (var ordinal = 1; ordinal <= group.Count; ordinal++)
            {
                string number;
                do
                {
                    sequence++;
                    if (sequence > 99)
                        throw new BusinessException(BusinessErrorType.Conflict, "ROOM_NUMBER_CAPACITY_EXCEEDED", "Dãy/tầng đã hết số phòng từ 01 đến 99.");
                    number = RoomNumbering.Format(code, group.Floor, sequence);
                } while (!occupied.Add(number));
                var values = overrides.TryGetValue((group.ClientKey, ordinal), out var special)
                    ? special.Values : group.Defaults;
                result.Add(new RoomBatchPreviewItem(group.ClientKey, ordinal, number, group.RowId, group.Floor, sequence, values));
            }
        }
        var hash = Hash(JsonSerializer.Serialize(new { plan.Groups, plan.Overrides, Assignments = result.Select(r => new { r.ClientKey, r.Ordinal, r.RoomNumber }) }));
        return new RoomBatchPreviewResult(result, hash);
    }

    /// <summary>Tạo mã băm SHA-256 cho dữ liệu đầu vào.</summary>
    private static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    /// <inheritdoc />
    public Task<RoomBatchPreviewResult> PreviewAsync(Guid propertyId, RoomBatchPlan plan, CancellationToken ct) =>
        BuildPreviewAsync(propertyId, plan, ct);

    /// <summary>Khóa việc cấp số phòng cho khu trọ trong transaction.</summary>
    private async Task AcquirePropertyLockAsync(Guid propertyId, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = _db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; SELECT @result;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = $"room-numbering:{propertyId:N}";
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (result < 0)
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_BATCH_BUSY", "Khu trọ đang được cấp số phòng. Vui lòng thử lại.");
    }

    /// <inheritdoc />
    public async Task<RoomBatchCreateResult> CreateAsync(Guid propertyId, string ownerId, RoomBatchPlan plan, string fingerprint, Guid idempotencyKey, CancellationToken ct)
    {
        var requestHash = Hash(JsonSerializer.Serialize(new { plan, fingerprint }));
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await AcquirePropertyLockAsync(propertyId, ct);
        var prior = await _db.RoomBatchSubmissions.AsNoTracking().SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.OwnerUserId == ownerId && x.IdempotencyKey == idempotencyKey, ct);
        if (prior is not null)
        {
            if (prior.RequestHash != requestHash)
                throw new BusinessException(BusinessErrorType.Conflict, "ROOM_BATCH_KEY_REUSED", "Lượt tạo này đã được dùng cho dữ liệu khác.");
            var originalRooms = JsonSerializer.Deserialize<List<Room>>(prior.ResultJson) ?? [];
            return new RoomBatchCreateResult(originalRooms
                .Select(room => new RoomResult(room, RoomStatus.Available)).ToList());
        }
        var property = await _db.Properties.AsNoTracking().SingleOrDefaultAsync(
            p => p.Id == propertyId && p.OwnerUserId == ownerId, ct);
        if (property is null || !property.IsActive)
            throw new BusinessException(BusinessErrorType.InvalidOperation, "PROPERTY_INACTIVE", "Khu trọ đã ngừng hoạt động.");
        var preview = await BuildPreviewAsync(propertyId, plan, ct);
        if (!string.Equals(preview.PreviewFingerprint, fingerprint, StringComparison.Ordinal))
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_PREVIEW_CHANGED", "Số phòng dự kiến đã thay đổi. Hãy xem trước và kiểm tra lại phòng đặc biệt.");

        var now = DateTime.UtcNow;
        var rooms = preview.Rooms.Select(item => new Room
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, RowId = item.RowId,
            Floor = item.Floor, SequenceNumber = item.SequenceNumber, RoomNumber = item.RoomNumber,
            RoomName = item.Values.RoomName?.Trim(), Area = item.Values.Area,
            BasePrice = item.Values.BasePrice, MaxOccupants = item.Values.MaxOccupants,
            Description = item.Values.Description?.Trim(), Status = RoomStatus.Available, CreatedAt = now
        }).ToList();
        _db.Rooms.AddRange(rooms);
        var maxima = preview.Rooms.GroupBy(r => RoomNumbering.Scope(r.RowId, r.Floor))
            .ToDictionary(g => g.Key, g => g.Max(r => r.SequenceNumber));
        foreach (var (scope, maximum) in maxima)
        {
            var counter = await _db.RoomNumberCounters.SingleOrDefaultAsync(
                c => c.PropertyId == propertyId && c.ScopeKey == scope, ct);
            if (counter is null)
                _db.RoomNumberCounters.Add(new RoomNumberCounter { PropertyId = propertyId, ScopeKey = scope, LastSequence = maximum });
            else counter.LastSequence = maximum;
        }
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessException(BusinessErrorType.Conflict, "ROOM_PREVIEW_CHANGED", "Số phòng đã thay đổi. Hãy xem trước lại.", ex);
        }
        _db.RoomBatchSubmissions.Add(new RoomBatchSubmission
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, OwnerUserId = ownerId,
            IdempotencyKey = idempotencyKey, RequestHash = requestHash,
            ResultJson = JsonSerializer.Serialize(rooms), CreatedAt = now
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new RoomBatchCreateResult(rooms.Select(r => new RoomResult(r, RoomStatus.Available)).ToList());
    }
}
