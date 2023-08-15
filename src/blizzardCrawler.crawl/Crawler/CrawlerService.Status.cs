using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    private ConcurrentDictionary<int, int> mainStatusCodes = new();
    private ConcurrentDictionary<int, int> retryStatusCodes = new();

    private void LogStatus()
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        CrawlerStatus status = GetCrawlerStatus();

        StringBuilder sb = new();
        sb.AppendLine($"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}");
        sb.AppendLine($"MainQueue: {status.MainQueueCount} (Threads: {status.MainThreads})");
        sb.AppendLine($"RetryQueue: {status.RetryQueueCount} (Threads: {status.RetryThreads})");
        sb.AppendLine("Main StatusCodes");
        sb.Append(string.Join(Environment.NewLine, status.MainStatusCodes.Select(s => $"StatusCode {s.Key} - {s.Value}")));
        sb.AppendLine();
        sb.AppendLine("Retry StatusCodes");
        sb.Append(string.Join(Environment.NewLine, status.RetryStatusCodes.Select(s => $"StatusCode {s.Key} - {s.Value}")));

        logger.LogInformation("{status}", sb.ToString());
    }

    public CrawlerStatus GetCrawlerStatus()
    {
        return new()
        {
            MainQueueCount = mainChannel.Reader.Count,
            RetryQueueCount = retryChannel.Reader.Count,
            MainThreads = mainConsumers.Count,
            RetryThreads = retryConsumers.Count,
            MainStatusCodes = mainStatusCodes.ToDictionary(),
            RetryStatusCodes = retryStatusCodes.ToDictionary()
        };
    }
}

public record CrawlerStatus
{
    public int MainQueueCount { get; set; }
    public int RetryQueueCount { get; set; }
    public int MainThreads { get; set; }
    public int RetryThreads { get; set; }
    public Dictionary<int, int> MainStatusCodes { get; set; } = new();
    public Dictionary<int, int> RetryStatusCodes { get; set; } = new();
}