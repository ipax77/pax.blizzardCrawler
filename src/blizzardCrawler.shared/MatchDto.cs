namespace blizzardCrawler.shared;

public record MatchDto
{
    public string Map { get; set; } = string.Empty;
    public long MatchDateUnixTimestamp { get; set; }
    public Decision Decision { get; set; }
    public Region Region { get; set; }
}
