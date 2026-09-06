using System.Threading.Channels;
using WebDiskTree.Core.Models;

namespace WebDiskTree.Infrastructure.Media;

public class ImdbLookupQueue
{
    private readonly Channel<ImdbLookupRequest> _channel = Channel.CreateUnbounded<ImdbLookupRequest>();

    public ChannelReader<ImdbLookupRequest> Reader => _channel.Reader;

    public void Enqueue(ImdbLookupRequest request) => _channel.Writer.TryWrite(request);
}
