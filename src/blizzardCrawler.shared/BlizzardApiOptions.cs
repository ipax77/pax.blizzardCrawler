namespace blizzardCrawler.shared;

/// <summary>
/// Represents the configuration options for the Blizzard API.
/// </summary>
public record BlizzardAPIOptions
{
    /// <summary>
    /// Gets or sets the client (optional)
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID used for API authentication.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret used for API authentication.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the log file for API request logging.
    /// </summary>
    public string LogFile { get; set; } = "crawler.log";

    /// <summary>
    /// Gets or sets the number of threads used for crawling.
    /// </summary>
    public int CrawlerThreadsCount { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of API requests allowed per second.
    /// </summary>
    public int MaxRequestsPerSecond { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of API requests allowed per hour.
    /// </summary>
    public int MaxRequestsPerHour { get; set; } = 36000;

    /// <summary>
    /// Gets or sets the timeout duration for HTTP requests in seconds.
    /// </summary>
    public int HttpRequestTimeoutInSeconds { get; set; } = 10;
    /// <summary>
    /// True: job lasts till the cancellationToken is canceled. False: stops if all players are processed
    /// </summary>
    public bool KeepRunning { get; set; }
}
