using System.Collections.Concurrent;

namespace BriAgent.Backend.Services;

public enum ApprovalStatus { Pending, Approved, Rejected }

public record ApprovalRequest(string ApprovalId, ApprovalStatus Status, DateTimeOffset CreatedAt, string? Reason = null);

public static class ApprovalStore
{
    private static readonly ConcurrentDictionary<string, ApprovalRequest> _store = new();
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<ApprovalStatus>> _waiters = new();

    public static ApprovalRequest Create()
    {
        var id = Guid.NewGuid().ToString("N");
        var req = new ApprovalRequest(id, ApprovalStatus.Pending, DateTimeOffset.UtcNow);
        _store[id] = req;
        _waiters[id] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return req;
    }

    public static ApprovalRequest? Get(string id)
        => _store.TryGetValue(id, out var r) ? r : null;

    public static bool Approve(string id)
    {
        if (!_store.TryGetValue(id, out var r)) return false;
        // Idempotencia: solo permitir transición si está Pending
        if (r.Status != ApprovalStatus.Pending) return false;
        var updated = r with { Status = ApprovalStatus.Approved };
        _store[id] = updated;
        if (_waiters.TryGetValue(id, out var tcs)) tcs.TrySetResult(ApprovalStatus.Approved);
        return true;
    }

    public static bool Reject(string id, string? reason = null)
    {
        if (!_store.TryGetValue(id, out var r)) return false;
        // Idempotencia: solo permitir transición si está Pending
        if (r.Status != ApprovalStatus.Pending) return false;
        var updated = r with { Status = ApprovalStatus.Rejected, Reason = reason };
        _store[id] = updated;
        if (_waiters.TryGetValue(id, out var tcs)) tcs.TrySetResult(ApprovalStatus.Rejected);
        return true;
    }

    public static Task<ApprovalStatus> WaitAsync(string id, CancellationToken ct)
    {
        if (_store.TryGetValue(id, out var r) && r.Status != ApprovalStatus.Pending)
        {
            return Task.FromResult(r.Status);
        }
        if (_waiters.TryGetValue(id, out var tcs))
        {
            using var reg = ct.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }
        // si no existe, devolver rechazado
        return Task.FromResult(ApprovalStatus.Rejected);
    }

    // Snapshot de approvals (útil para panel HITL)
    public static IReadOnlyList<ApprovalRequest> List()
        => _store.Values.ToArray();
}
