using System.Threading.Channels;
using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Scanning;

public class ScanQueue
{
    private readonly Channel<ScanJobRequest> _channel = Channel.CreateUnbounded<ScanJobRequest>();

    public ChannelReader<ScanJobRequest> Reader => _channel.Reader;

    public void Enqueue(ScanJobRequest request) => _channel.Writer.TryWrite(request);
}
