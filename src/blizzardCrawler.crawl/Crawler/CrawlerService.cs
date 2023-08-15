using blizzardCrawler.shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    private readonly IOptions<BlizzardAPIOptions> blizzardApiOptions;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<CrawlerService> logger;
    private readonly HttpClient httpClient;
    private readonly int MaxRequestsPerSecond;
    private readonly int MaxRequestsPerHour;
    private readonly int MaxHttpThreads;
    private readonly TokenBucket tokenBucketSeconds;
    private readonly TokenBucket tokenBucketHour;
    private readonly SemaphoreSlim ss;
    private CancellationToken cancellationToken = default;
    private readonly TimeSpan _requestTimeout;


    public CrawlerService(IOptions<BlizzardAPIOptions> blizzardApiOptions,
                          IMemoryCache memoryCache,
                          ILogger<CrawlerService> logger)
    {
        this.blizzardApiOptions = blizzardApiOptions;
        this.memoryCache = memoryCache;
        this.logger = logger;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = Timeout.InfiniteTimeSpan
        };
        
        httpClient = new HttpClient(handler);

        MaxRequestsPerSecond = blizzardApiOptions.Value.MaxRequestsPerSecond;
        MaxRequestsPerHour = blizzardApiOptions.Value.MaxRequestsPerHour;
        MaxHttpThreads = blizzardApiOptions.Value.CrawlerThreadsCount;
        
        tokenBucketSeconds = new(MaxRequestsPerSecond, 1000, 3000);
        tokenBucketHour = new(MaxRequestsPerHour, 3600000, 60000);
        
        _requestTimeout = TimeSpan.FromSeconds(Math.Max(1, blizzardApiOptions.Value.HttpRequestTimeoutInSeconds));

        var maxThreads = Math.Max(1, blizzardApiOptions.Value.CrawlerThreadsCount);
        ss = new(maxThreads, maxThreads);
    }

    public void StartJob(List<PlayerEtagIndex> players, CancellationToken token)
    {
        cancellationToken = token;
        AddPlayers(players);
        _ = StartTickThread();
    }

    private async Task StartTickThread()
    {
        await Task.Delay(1000);
        int i = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            i++;
            if (i % 10 == 0)
            {
                AdjustMainRetryConsumerCounts();
                LogStatus();
            }
            await Task.Delay(1000);
            if (mainChannel.Reader.Count == 0 && retryChannel.Reader.Count == 0)
            {
                break;
            }
        }
        logger.LogInformation("job done.");
        Cleanup();
    }

    private void Cleanup()
    {
        while (!mainConsumers.IsEmpty)
        {
            RemoveMainChannelConsumer();
        }

        while (!retryConsumers.IsEmpty)
        {
            RemoveRetryChannelConsumer();
        }

        mainStatusCodes.Clear();
        retryStatusCodes.Clear();
    }

    private async Task<int> HandlePlayer(PlayerEtagIndex player)
    {
        var result = await GetMatchHistory(player);

        if (result.StatusCode == 503)
        {
            AddRetryPlayer(player);
        }
        else if (result.StatusCode == 504 || result.StatusCode == 701) // timeout
        {
            AddRetryPlayer(player);
        }
        else if (result.StatusCode == 777 || result.StatusCode == 778) // tokenBucket exceeded
        {
            AddRetryPlayer(player);
        }
        else if (result.StatusCode == 429) // too many requests
        {
            // await Task.Delay(60000);
            AddRetryPlayer(player);
        }
        return result.StatusCode;
    }

    private void AdjustMainRetryConsumerCounts()
    {
        int retryCount = retryChannel.Reader.Count;

        int retryShould;
        if (mainChannel.Reader.Count == 0)
        {
            retryShould = MaxHttpThreads;
        }
        else
        {
            retryShould = retryCount switch
            {
                > 1000 => MaxHttpThreads,
                > 500 => MaxHttpThreads / 2,
                > 250 => MaxHttpThreads / 3,
                > 100 => MaxHttpThreads / 4,
                < 1 => 0,
                < 10 => 1,
                _ => 1
            };
        }

        int mainShould = MaxHttpThreads - retryShould;

        if (mainConsumers.Count > mainShould)
        {
            while(mainConsumers.Count > mainShould)
            {
                RemoveMainChannelConsumer();
            }
        }
        else if (mainConsumers.Count < mainShould)
        {
            while(mainConsumers.Count < mainShould)
            {
                AddMainChannelConsumer();
            }
        }

        if (retryConsumers.Count > retryShould)
        {
            while(retryConsumers.Count > retryShould)
            {
                RemoveRetryChannelConsumer();
            }
        }
        else if (retryConsumers.Count < retryShould)
        {
            while(retryConsumers.Count < retryShould)
            {
                AddRetryChannelConsumer();
            }
        }
    }
}
