namespace blizzardCrawler.services;
public record BlMatch
{
    public string Map { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public long Date { get; set; }
}
