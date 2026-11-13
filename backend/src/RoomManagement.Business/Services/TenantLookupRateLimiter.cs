using System.Collections.Concurrent;

namespace RoomManagement.Business.Services;

/// <summary>
/// Giới hạn tần suất tra cứu Tenant theo cặp SĐT + CCCD cho mỗi actor để chống dò dữ liệu.
/// </summary>
public sealed class TenantLookupRateLimiter
{
    private const int MaxRequestsPerWindow = 20;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _buckets = new(StringComparer.Ordinal);

    public bool TryAcquire(string actorUserId)
    {
        var now = DateTime.UtcNow;
        var queue = _buckets.GetOrAdd(actorUserId, _ => new Queue<DateTime>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > Window)
                queue.Dequeue();

            if (queue.Count >= MaxRequestsPerWindow)
                return false;

            queue.Enqueue(now);
            return true;
        }
    }
}
