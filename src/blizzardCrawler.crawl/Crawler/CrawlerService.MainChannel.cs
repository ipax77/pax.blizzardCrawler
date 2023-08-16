
using blizzardCrawler.shared;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    private Channel<PlayerEtagIndex> mainChannel = Channel.CreateUnbounded<PlayerEtagIndex>();
    private ConcurrentBag<CancellationTokenSource> mainConsumers = new();

    public bool AddPlayer(PlayerEtagIndex player)
    {
        if (!started)
        {
            return false;
        }
        if (mainChannel.Writer.TryWrite(player))
        {
            Interlocked.Increment(ref jobs);
            return true;
        }
        return false;
    }

    private void AddPlayers(List<PlayerEtagIndex> players)
    {
        foreach (var player in players.OrderByDescending(o => o.LatestMatchInfo))
        { 
            mainChannel.Writer.TryWrite(player);
        }
    }

    private void CreateMainChannelConsumers()
    {
        for (int i = 0; i < apiOptions.CrawlerThreadsCount; i++)
        {
            AddMainChannelConsumer();
        }
    }

    private void RemoveMainChannelConsumer()
    {
        if (mainConsumers.TryTake(out CancellationTokenSource? consumer)
            && consumer is not null)
        {
            consumer.Cancel();
            consumer.Dispose();
        }
    }

    private void AddMainChannelConsumer()
    {
        if (mainConsumers.Count < apiOptions.CrawlerThreadsCount)
        {
            CancellationTokenSource cts = new();
            mainConsumers.Add(cts);
            _ = MainChannelConsumer(cts.Token);
        }
    }

    private async Task MainChannelConsumer(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (mainChannel.Reader.TryRead(out PlayerEtagIndex? player)
                && player is not null)
            {
                var statusCode = await HandlePlayer(player);
                mainStatusCodes.AddOrUpdate(statusCode, 1, (k, v) => ++v);
            }
            else
            {
                await Task.Delay(1000, token);
            }
        }
    }
}
