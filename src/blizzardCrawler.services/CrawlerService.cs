using blizzardCrawler.shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace blizzardCrawler.services;

public partial class CrawlerService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptions<BlizzardAPIOptions> options;
    private readonly IOptions<DbImportOptions> importOptions;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<CrawlerService> logger;


    public CrawlerService(IHttpClientFactory httpClientFactory,
                          IOptions<BlizzardAPIOptions> options,
                          IOptions<DbImportOptions> importOptions,
                          IServiceScopeFactory scopeFactory,
                          IMemoryCache memoryCache,
                          ILogger<CrawlerService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options;
        this.importOptions = importOptions;
        this.scopeFactory = scopeFactory;
        this.memoryCache = memoryCache;
        this.logger = logger;
        // this.httpClient = httpClientFactory.CreateClient();

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = Timeout.InfiniteTimeSpan
        };
        httpClient = new HttpClient(handler);

        tokenBucketSeconds = new(MaxRequestsPerSecond, 1000, 1000);
        tokenBucketHour = new(MaxRequestsPerHour, 3600000, 10000);
        CrawlerThreads = Math.Max(1, (int)(importOptions.Value.CrawlerThreadsCount * 0.60));
        RetryThreads = Math.Max(1, importOptions.Value.CrawlerThreadsCount - CrawlerThreads);
        ss = new(CrawlerThreads + RetryThreads);
    }

    private readonly HttpClient httpClient;

    private SemaphoreSlim ssToken = new(1, 1);
    private readonly int MaxRequestsPerSecond = 100;
    private readonly int MaxRequestsPerHour = 36000;
    private readonly TokenBucket tokenBucketSeconds;
    private readonly TokenBucket tokenBucketHour;
    private readonly int CrawlerThreads;
    private readonly int RetryThreads;
    private readonly SemaphoreSlim ss;

    public void StartJob(CancellationToken token = default)
    {
        _ = Job(token);
    }

    private async Task Job(CancellationToken token = default)
    {
        using var scope = scopeFactory.CreateScope();
        var playerRepository = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var matchRepository = scope.ServiceProvider.GetRequiredService<MatchRepository>();

        var players = await playerRepository.GetPlayers();

        logger.LogInformation("starting job with {count} players", players.Count);

        ParallelOptions parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = CrawlerThreads,
            CancellationToken = token
        };

        for (int j = 0; j < 10; j++)
        {
            int i = 0;
            await Parallel.ForEachAsync(players, parallelOptions, async (player, token) =>
            {
                var etag = playerRepository.GetPlayerEtag(player);
                await HandlePlayer(player,
                                   etag,
                                   false,
                                   playerRepository,
                                   matchRepository,
                                   token);
                Interlocked.Increment(ref i);
                if (i % 1000 == 0)
                {
                    logger.LogInformation("players crawled: {i}", i);
                    playerRepository.LogCrawlStatus(retryChannel.Reader.Count);
                }
            });

            logger.LogInformation("players crawled: {i}", i);
            playerRepository.LogCrawlStatus(retryChannel.Reader.Count);
            await matchRepository.StoreMatches();

            while (retryChannel.Reader.Count > 10)
            {
                logger.LogDebug("waiting for retry channel {count}", retryChannel.Reader.Count);
                await Task.Delay(10000, token);
            }
        }
        logger.LogInformation("job done. (cancleled: {canceled})", token.IsCancellationRequested);
    }

    private async Task<int> HandlePlayer(PlayerIndex player,
                                    string? etag,
                                    bool retry,
                                    PlayerRepository playerRepository,
                                    MatchRepository matchRepository,
                                    CancellationToken token)
    {

        (var matches, int statusCode, var newEtag)
            = await GetMatchHistory(player, etag, retry, dsOnly: true, token);

        playerRepository.SetCrawlInfo(player, GetLatestMatchDate(matches), newEtag, statusCode);
        matchRepository.StorePlayerMatches(player, matches);

        logger.LogDebug("Got status code {statusCode}", statusCode);
        if (statusCode == 503)
        {
            tokenBucketHour.ReAddTokenAfterServiceUnavailable();
            tokenBucketSeconds.ReAddTokenAfterServiceUnavailable();
            RetryPlayer(player, etag, token);
        }
        else if (statusCode == 701) // timeout
        {
            RetryPlayer(player, etag, token);
        }

        return statusCode;
    }

    private DateTime GetLatestMatchDate(List<BlMatch>? matches)
    {
        if (matches is null || matches.Count == 0)
        {
            return DateTime.MinValue;
        }
        return DateTimeOffset.FromUnixTimeSeconds(matches.Last().Date).UtcDateTime;
    }

    public async Task GetPlayerMatchInfos(PlayerIndex player, string? eTag)
    {
        (var matches, int statusCode, var etag) = await GetMatchHistory(player, eTag, false);
        logger.LogInformation("indahouse2");
    }
}

internal record TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal record CsvPlayer
{
    public int ToonId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
}