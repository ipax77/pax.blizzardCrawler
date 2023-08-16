using blizzardCrawler.crawl.Crawler;
using blizzardCrawler.shared;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace blizzardCrawler.crawl;

public class CrawlerHandler : ICrawlerHandler
{
    private readonly IServiceScopeFactory scopeFactory;

    public CrawlerHandler(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    public ICrawlerService? LatestCrawlerService { get; private set; }

    public async IAsyncEnumerable<MatchInfoResult> GetMatchInfos(List<PlayerEtagIndex> players,
                                                              BlizzardAPIOptions options,
                                                              TokenBucket? tbSecond = null,
                                                              TokenBucket? tbHour = null,
                                                              [EnumeratorCancellation] CancellationToken token = default)
    {
        using var scope = scopeFactory.CreateScope();
        var crawlerService = scope.ServiceProvider.GetRequiredService<ICrawlerService>();
        LatestCrawlerService = crawlerService;

        TokenBucket mytbSecond = tbSecond is null ? new(options.MaxRequestsPerSecond, 1000, 3000) : tbSecond;
        TokenBucket mytbHour = tbHour is null ? new(options.MaxRequestsPerHour, 3600000, 60000) : tbHour;

        var channel = Channel.CreateUnbounded<MatchInfoEventArgs>();

        crawlerService.MatchInfoReady += (s, o) =>
        {
            channel.Writer.TryWrite(o);
        };
        crawlerService.JobDone += (s, o) =>
        {
            channel.Writer.Complete();
        };

        crawlerService.StartJob(players, options, mytbSecond, mytbHour, token);

        while (await channel.Reader.WaitToReadAsync(token))
        {
            while (channel.Reader.TryRead(out var matchInfo))
            {
                yield return new(matchInfo);
            }
        }
    }

    public CrawlerStatus? GetLatestCrawlerStatus()
    {
        return LatestCrawlerService?.GetCrawlerStatus();
    }
}

