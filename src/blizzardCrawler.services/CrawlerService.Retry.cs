using blizzardCrawler.shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace blizzardCrawler.services;

public partial class CrawlerService
{
    private Channel<PlayerRetryIndex> retryChannel = Channel.CreateUnbounded<PlayerRetryIndex>();
    private readonly object retrylockobject = new();
    private bool retryConsuming;

    private void RetryPlayer(PlayerIndex player, string? etag, CancellationToken token)
    {
        retryChannel.Writer.TryWrite(new(player, etag));
        _ = ConsumeRetryChannel(token);
    }

    private void WritePlayer(PlayerIndex player, string? etag, CancellationToken token)
    {
        retryChannel.Writer.TryWrite(new(player, etag));
        _ = ConsumeRetryChannel(token);
    }

    private async Task ConsumeRetryChannel(CancellationToken token)
    {
        lock (retrylockobject)
        {
            if (retryConsuming)
            {
                return;
            }
            retryConsuming = true;
        }
        using var scope = scopeFactory.CreateScope();
        var playerRepository = scope.ServiceProvider.GetRequiredService<PlayerRepository>();
        var matchRepository = scope.ServiceProvider.GetRequiredService<MatchRepository>();

        List<Task> consumerTasks = new();
        for (int i = 0; i < RetryThreads; i++)
        {
            var task = Task.Run(async () =>
            {
                await RetryConsumer(playerRepository, matchRepository, token);
            });
            consumerTasks.Add(task);
        }
        logger.LogInformation("Retry consumers started: {count}", RetryThreads);
        
        await Task.WhenAll(consumerTasks);

        retryConsuming = false;
    }

    private async Task RetryConsumer(PlayerRepository playerRepository, MatchRepository matchRepository, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (retryChannel.Reader.TryRead(out PlayerRetryIndex? playerRetry)
                && playerRetry is not null)
            {
                var statusCode = await HandlePlayer(playerRetry.GetPlayerIndex(),
                                                    playerRetry.Etag,
                                                    true,
                                                    playerRepository,
                                                    matchRepository,
                                                    token);
                
                //int retries = playerRepository.GetPlayer503s(playerRetry.GetPlayerIndex());
                //if (statusCode == 200 || statusCode == 304)
                //{
                //    logger.LogDebug("player succeeded after {count} attempts.", retries);
                //}
                //else if (retries > 10)
                //{
                //    logger.LogWarning("player still failing after {count} attempts.", retries);
                //}
            }
            else
            {
                await Task.Delay(1000, token);
            }
        }
    }
}

public record PlayerRetryIndex
{
    public PlayerRetryIndex(PlayerIndex player, string? etag)
    {
        ToonId = player.ToonId;
        RegionId = player.RegionId;
        RealmId = player.RealmId;
        Etag = etag;

    }
    public int ToonId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
    public string? Etag { get; set; }

    public PlayerIndex GetPlayerIndex()
    {
        return new(ToonId, RegionId, RealmId);
    }
}