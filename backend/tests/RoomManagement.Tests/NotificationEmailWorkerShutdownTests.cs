using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoomManagement.API.Infrastructure;

namespace RoomManagement.Tests;

public sealed class NotificationEmailWorkerShutdownTests
{
    [Fact]
    public async Task Disposed_logger_factory_during_scope_resolution_does_not_fault_worker_when_stopping()
    {
        using var scopes = new InterruptedScopeFactory();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SendGrid:ApiKey"] = "test-only",
            ["SendGrid:FromEmail"] = "test@example.test"
        }).Build();
        using var worker = new NotificationEmailWorker(scopes, new UnusedHttpClientFactory(),
            configuration, NullLogger<NotificationEmailWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await worker.StartAsync(timeout.Token);
        try
        {
            await scopes.Entered.Task.WaitAsync(timeout.Token);
            var stop = worker.StopAsync(timeout.Token);
            scopes.Continue.Set();
            await stop;
            Assert.True(worker.ExecuteTask?.IsCompletedSuccessfully);
        }
        finally
        {
            scopes.Continue.Set();
        }
    }

    private sealed class InterruptedScopeFactory : IServiceScopeFactory, IDisposable
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Continue { get; } = new();

        public IServiceScope CreateScope()
        {
            Entered.TrySetResult();
            if (!Continue.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The shutdown test did not release service resolution.");
            throw new ObjectDisposedException("LoggerFactory");
        }

        public void Dispose() => Continue.Dispose();
    }

    private sealed class UnusedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("The shutdown test must never send email.");
    }
}
