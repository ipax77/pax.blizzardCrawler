using blizzardCrawler.shared;

namespace blizzardCrawler.crawl.Crawler
{
    public interface ICrawlerService
    {
        /// <summary>
        /// Adds a player to the queue if the job is still running
        /// </summary>
        bool AddPlayer(PlayerEtagIndex player);
        CrawlerStatus GetCrawlerStatus();
        /// <summary>
        /// Starts the crawl job. This can be done only once per instance.
        /// </summary>
        /// <param name="players">Players to process</param>
        /// <param name="apiOptions">Configuration options for the Blizzard API.</param>
        /// <param name="tokenBucketSecond">Token bucket for rate limiting at the per-second rate.</param>
        /// <param name="tokenBucketHour">Token bucket for rate limiting at the per-hour rate.</param>
        /// <param name="token">Cancellation token for the job.</param>
        void StartJob(List<PlayerEtagIndex> players,
                      BlizzardAPIOptions apiOptions,
                      TokenBucket tokenBucketSecond,
                      TokenBucket tokenBucketHour,
                      CancellationToken token = default);
        /// <summary>
        /// Event that returns the crawled matchinfos per player
        /// </summary>
        event EventHandler<MatchInfoEventArgs>? MatchInfoReady;
        /// <summary>
        /// Job done event (all players processed / queues empty)
        /// </summary>
        event EventHandler? JobDone;
    }
}