namespace blizzardCrawler.shared;

public record DbImportOptions
{
    public string ImportConnectionString { get; set; } = string.Empty;
    public string DsstatsConnectionString { get; set; } = string.Empty;
    public string ArcadeConnectionString { get; set; } = string.Empty;
    public string LogFile { get; set; } = "log.txt";
    public int CrawlerThreadsCount { get; set; } = 30;
}
