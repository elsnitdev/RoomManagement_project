using Microsoft.EntityFrameworkCore;
using RoomManagement.Business.Interfaces;
using RoomManagement.Data;

namespace RoomManagement.API.Infrastructure;

public sealed class BlobCleanupWorker(IServiceScopeFactory scopes, ILogger<BlobCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RoomManagementDbContext>();
                var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var jobs = await db.BlobCleanupJobs.Where(x => x.NextAttemptAt <= DateTime.UtcNow)
                    .OrderBy(x => x.CreatedAt).Take(20).ToListAsync(stoppingToken);
                foreach (var job in jobs)
                {
                    try { await storage.DeleteAsync(job.BlobName, stoppingToken); db.BlobCleanupJobs.Remove(job); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        job.Attempts++;
                        job.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, 1 << Math.Min(job.Attempts, 6)));
                        logger.LogWarning(ex, "Blob cleanup failed: {JobId}", job.Id);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested) break;
                logger.LogError(ex, "Blob cleanup cycle failed");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
