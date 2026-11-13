using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RoomManagement.Business.DTOs;
using RoomManagement.Business.Errors;
using RoomManagement.Domain.Entities;
using RoomManagement.Domain.Enums;

namespace RoomManagement.Business.Services;

public static class SettlementChargeIdentity
{
    public static Invoice? VerifyRetry(Invoice? existing, string requestHash)
    {
        if (existing is null) return null;
        if (existing.SettlementRequestHash == requestHash) return existing;
        throw new BusinessException(BusinessErrorType.Conflict, "SETTLEMENT_OPERATION_KEY_CONFLICT",
            "Mã thao tác đã được dùng cho hóa đơn có nội dung khác.");
    }

    public static string RequestHash(Guid contractId, InvoiceType type, DateOnly dueDate,
        string? note, IReadOnlyList<EditableInvoiceItem> items) => Hash(new
    {
        contractId, type, dueDate, note = note?.Trim(), items = CanonicalItems(items)
    });

    public static string ChargeFingerprint(Guid contractId, InvoiceType type,
        IReadOnlyList<EditableInvoiceItem> items) => Hash(new
    {
        contractId, type, items = CanonicalItems(items)
    });

    private static object[] CanonicalItems(IReadOnlyList<EditableInvoiceItem> items) =>
        items.Select(x => new { x.Type, Description = x.Description.Trim(), x.Quantity, x.UnitPrice })
            .OrderBy(x => x.Type).ThenBy(x => x.Description, StringComparer.Ordinal)
            .ThenBy(x => x.Quantity).ThenBy(x => x.UnitPrice)
            .Cast<object>().ToArray();

    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
