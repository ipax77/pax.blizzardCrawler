
using blizzardCrawler.shared;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    private Channel<PlayerEtagIndex> mainChannel = Channel.CreateUnbounded<PlayerEtagIndex>();
    private ConcurrentBag<CancellationTokenSource> mainConsumers = new();
    public void AddPlayers(List<PlayerEtagIndex> players)
    {
        foreach (var player in players.OrderByDescending(o => o.LatestMatchInfo))
        { 
            mainChannel.Writer.TryWrite(player);
        }
    }

    public void AddPlayer(PlayerEtagIndex player)
    {
        mainChannel.Writer.TryWrite(player);
    }

    private void CreateMainChannelConsumers()
    {
        for (int i = 0; i < MaxHttpThreads; i++)
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
        if (mainConsumers.Count < MaxHttpThreads)
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
