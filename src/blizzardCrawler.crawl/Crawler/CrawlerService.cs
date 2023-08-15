using blizzardCrawler.shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService : ICrawlerService
{
    private static readonly SemaphoreSlim ssToken = new(1, 1);

    private readonly IMemoryCache memoryCache;
    private readonly ILogger<CrawlerService> logger;
    private readonly HttpClient httpClient;
    private TokenBucket? tokenBucketSecond;
    private TokenBucket? tokenBucketHour;
    private CancellationToken cancellationToken = default;
    private TimeSpan _requestTimeout;
    private BlizzardAPIOptions apiOptions = new();
    private SemaphoreSlim ss;
    private bool started;

    public event EventHandler<MatchInfoEventArgs>? MatchInfoReady;
    public event EventHandler? JobDone;
    protected virtual void OnMatchInfoReady(MatchInfoEventArgs e)
    {
        MatchInfoReady?.Invoke(this, e);
    }

    protected virtual void OnJobDone(EventArgs e)
    {
        JobDone?.Invoke(this, e);
    }

    public CrawlerService(IMemoryCache memoryCache,
                          ILogger<CrawlerService> logger)
    {
        this.memoryCache = memoryCache;
        this.logger = logger;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = Timeout.InfiniteTimeSpan
        };

        httpClient = new HttpClient(handler);
        _requestTimeout = TimeSpan.FromSeconds(Math.Max(1, 10));
        ss = new(30, 30);
    }

    public void StartJob(List<PlayerEtagIndex> players,
                         BlizzardAPIOptions apiOptions,
                         TokenBucket tokenBucketSecond,
                         TokenBucket tokenBucketHour,
                         CancellationToken token = default)
    {
        this.apiOptions = apiOptions;

        this.tokenBucketSecond = tokenBucketSecond;
        this.tokenBucketHour = tokenBucketHour;

        ss?.Dispose();
        ss = new(apiOptions.CrawlerThreadsCount, apiOptions.CrawlerThreadsCount);
        _requestTimeout = TimeSpan.FromSeconds(apiOptions.HttpRequestTimeoutInSeconds);

        cancellationToken = token;
        AddPlayers(players);
        _ = StartTickThread();
        started = true;
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
                OnJobDone(new());
                if (!apiOptions.KeepRunning)
                {
                    break;
                }
            }
        }
        logger.LogInformation("job done.");
        Cleanup();
        started = false;
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
        var response = await GetMatchHistory(player);
        // var response = await MockGetMatchHistory(player);

        if (response.StatusCode == 503)
        {
            AddRetryPlayer(player);
        }
        else if (response.StatusCode == 504 || response.StatusCode == 701) // timeout
        {
            AddRetryPlayer(player);
        }
        else if (response.StatusCode == 777 || response.StatusCode == 778) // tokenBucket exceeded
        {
            AddRetryPlayer(player);
        }
        else if (response.StatusCode == 429) // too many requests
        {
            logger.LogWarning("Too Many Requests (429), SecondTokens: {tokensSecond}, HourTokens: {tokensHour}}",
                tokenBucketSecond?.CurrentTokens(),
                tokenBucketHour?.CurrentTokens());
            await Task.Delay(60000);
            AddRetryPlayer(player);
        }
        else
        {
            OnMatchInfoReady(GetMatchInfoResult(player, response));
        }

        return response.StatusCode;
    }

    private MatchInfoEventArgs GetMatchInfoResult(PlayerEtagIndex player, MatchResponse response)
    {
        DateTime latestMatchInfo = response.Matches.Count == 0 ? DateTime.MinValue :
            DateTimeOffset.FromUnixTimeSeconds(response.Matches.Max(m => m.Date)).UtcDateTime;

        return new()
        {
            Player = player with { Etag = response.Etag, LatestMatchInfo = latestMatchInfo },
            MatchInfos = response.Matches.Select(s => new MatchDto()
            {
                Map = s.Map,
                MatchDateUnixTimestamp = s.Date,
                Decision = GetDecision(s.Decision),
                Region = (Region)player.RegionId
            }).ToList(),
            StatusCode = response.StatusCode,
        };
    }

    private Decision GetDecision(string decisionString)
    {
        if (Enum.TryParse(decisionString, out Decision decision))
        {
            return decision;
        }
        return Decision.None;
    }
}
