using blizzardCrawler.crawl.Crawler;
using blizzardCrawler.shared;

namespace blizzardCrawler.crawl
{
    public interface ICrawlerHandler
    {
        /// <summary>
        /// Get Matchinfos from the Blizzard API. If you use multiple calls with the same Blizzard Client you should share the TokenBuckets
        /// </summary>
        /// <param name="players"></param>
        /// <param name="options"></param>
        /// <param name="tbSecond">If you use multiple calls with the same Blizzard Client you should share the TokenBuckets</param>
        /// <param name="tbHour">If you use multiple calls with the same Blizzard Client you should share the TokenBuckets</param>
        /// <param name="token"></param>
        /// <returns></returns>
        IAsyncEnumerable<MatchInfoResult> GetMatchInfos(List<PlayerEtagIndex> players,
                                                        BlizzardAPIOptions options,
                                                        TokenBucket? tbSecond = null,
                                                        TokenBucket? tbHour = null,
                                                        CancellationToken token = default);
        /// <summary>
        /// Status of the latest CrawlerService used for GetMatchInfos
        /// </summary>
        /// <returns></returns>
        CrawlerStatus? GetLatestCrawlerStatus();
    }
}