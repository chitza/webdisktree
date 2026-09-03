using System.Collections.Concurrent;

namespace WebDiskTree.Infrastructure.Scanning;

public class ScanCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    public CancellationTokenSource Register(Guid scanId)
    {
        var cts = new CancellationTokenSource();
        _tokens[scanId] = cts;
        return cts;
    }

    public bool TryCancel(Guid scanId)
    {
        if (_tokens.TryGetValue(scanId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    public void Remove(Guid scanId)
    {
        if (_tokens.TryRemove(scanId, out var cts))
        {
            cts.Dispose();
        }
    }
}
