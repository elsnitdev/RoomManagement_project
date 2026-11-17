using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Services;
using RoomManagement.Data;
using RoomManagement.Domain.Enums;

namespace RoomManagement.API.Infrastructure;

public sealed class ContractExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ContractExpiryWorker> _logger;

    public ContractExpiryWorker(IServiceScopeFactory scopes, ILogger<ContractExpiryWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
                var draftExpiry = scope.ServiceProvider.GetRequiredService<ContractDraftExpiryService>();
                var (cancelled, needsReview) = await draftExpiry.CancelInactiveAsync(DateTime.UtcNow, stoppingToken);
                if (cancelled > 0 || needsReview > 0)
                    _logger.LogInformation("Đã tự hủy {Cancelled} bản nháp; {NeedsReview} bản nháp cần đối soát cọc.",
                        cancelled, needsReview);
                var today = VietnamDate.Today;
                await db.Contracts.Where(c => c.Status == ContractStatus.PendingStart && c.StartDate <= today &&
                    (c.EndDate == null || c.EndDate >= today) &&
                    db.Rooms.Any(r => r.Id == c.RoomId && r.Status == RoomStatus.Available))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContractStatus.Active)
                        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), stoppingToken);
                await db.Contracts.Where(c => c.Status == ContractStatus.PendingStart && c.EndDate != null && c.EndDate < today)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContractStatus.Expired)
                        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), stoppingToken);
                await db.Contracts.Where(c => c.Status == ContractStatus.Active &&
                    c.EndDate != null && c.EndDate < today)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContractStatus.Expired)
                        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested) break;
                try { _logger.LogError(ex, "Không thể cập nhật hợp đồng hết hạn."); }
                catch (ObjectDisposedException) { break; }
                catch (AggregateException logError) when (
                    logError.Flatten().InnerExceptions.All(error => error is ObjectDisposedException))
                {
                    break;
                }
            }
            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
