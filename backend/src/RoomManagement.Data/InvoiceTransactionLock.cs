using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RoomManagement.Business.Errors;

namespace RoomManagement.Data;

internal static class InvoiceTransactionLock
{
    public static async Task AcquireAsync(RoomManagementDbContext db, Guid invoiceId, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Phải mở transaction trước khi khóa invoice.");
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = $"invoice:{invoiceId:N}";
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (result < 0)
            throw new BusinessException(BusinessErrorType.Conflict,
                "INVOICE_BUSY", "Hóa đơn đang được xử lý. Vui lòng thử lại.");
    }
}
