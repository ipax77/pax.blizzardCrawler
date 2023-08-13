namespace blizzardCrawler.shared;

public record MatchDto
{
    public long MatchDateUnixTimestamp { get; set; }
    public Decision Decision { get; set; }
    public Region Region { get; set; }
}
