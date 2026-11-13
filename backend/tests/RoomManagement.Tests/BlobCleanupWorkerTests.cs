using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoomManagement.API.Infrastructure;

namespace RoomManagement.Tests;

public sealed class BlobCleanupWorkerTests
{
    [Fact]
    public async Task StopWhileWaitingForNextTickCompletesNormally()
    {
        var scopes = new FailingScopeFactory();
        using var worker = new BlobCleanupWorker(scopes, NullLogger<BlobCleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await scopes.CycleStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        Assert.False(worker.ExecuteTask?.IsCompleted);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.ExecuteTask?.IsCompletedSuccessfully);
    }

    private sealed class FailingScopeFactory : IServiceScopeFactory
    {
        public TaskCompletionSource CycleStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IServiceScope CreateScope()
        {
            CycleStarted.TrySetResult();
            throw new InvalidOperationException("Simulated cleanup cycle failure");
        }
    }
}
