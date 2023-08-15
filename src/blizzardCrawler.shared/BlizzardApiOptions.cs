namespace blizzardCrawler.shared;

public record BlizzardAPIOptions
{
    public string ClientName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string LogFile { get; set; } = "crawler.log";
    public int CrawlerThreadsCount { get; set; }
    public int MaxRequestsPerSecond { get; set; } = 100;
    public int MaxRequestsPerHour { get; set; } = 36000;
    public int HttpRequestTimeoutInSeconds { get; set; } = 10;
}