namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    private void AdjustMainRetryConsumerCounts()
    {
        int retryCount = retryChannel.Reader.Count;

        int retryShould;
        if (mainChannel.Reader.Count == 0)
        {
            retryShould = apiOptions.CrawlerThreadsCount;
        }
        else
        {
            retryShould = retryCount switch
            {
                > 1000 => apiOptions.CrawlerThreadsCount,
                > 500 => apiOptions.CrawlerThreadsCount / 2,
                > 250 => apiOptions.CrawlerThreadsCount / 3,
                > 100 => apiOptions.CrawlerThreadsCount / 4,
                < 1 => 0,
                < 10 => 1,
                _ => 1
            };
        }

        int mainShould = apiOptions.CrawlerThreadsCount - retryShould;

        if (mainConsumers.Count > mainShould)
        {
            while (mainConsumers.Count > mainShould)
            {
                RemoveMainChannelConsumer();
            }
        }
        else if (mainConsumers.Count < mainShould)
        {
            while (mainConsumers.Count < mainShould)
            {
                AddMainChannelConsumer();
            }
        }

        if (retryConsumers.Count > retryShould)
        {
            while (retryConsumers.Count > retryShould)
            {
                RemoveRetryChannelConsumer();
            }
        }
        else if (retryConsumers.Count < retryShould)
        {
            while (retryConsumers.Count < retryShould)
            {
                AddRetryChannelConsumer();
            }
        }
    }
}
