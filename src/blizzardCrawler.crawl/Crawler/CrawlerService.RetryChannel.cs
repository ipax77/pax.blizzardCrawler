using blizzardCrawler.shared;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    private Channel<PlayerEtagIndex> retryChannel = Channel.CreateUnbounded<PlayerEtagIndex>();
    private ConcurrentBag<CancellationTokenSource> retryConsumers = new();

    private void AddRetryPlayer(PlayerEtagIndex player)
    {
        retryChannel.Writer.TryWrite(player);
    }

    private void CreateRetryChannelConsumers()
    {
        AddRetryChannelConsumer();
    }

    private void RemoveRetryChannelConsumer()
    {
        if (retryConsumers.TryTake(out CancellationTokenSource? consumer)
            && consumer is not null)
        {
            consumer.Cancel();
            consumer.Dispose();
        }
    }

    private void AddRetryChannelConsumer()
    {
        if (retryConsumers.Count < apiOptions.CrawlerThreadsCount)
        {
            CancellationTokenSource cts = new();
            retryConsumers.Add(cts);
            _ = RetryChannelConsumer(cts.Token);
        }
    }

    private async Task RetryChannelConsumer(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (retryChannel.Reader.TryRead(out PlayerEtagIndex? player)
                && player is not null)
            {
                var statusCode = await HandlePlayer(player);
                retryStatusCodes.AddOrUpdate(statusCode, 1, (k, v) => ++v);
            }
            else
            {
                await Task.Delay(1000, token);
            }
        }
    }
}
