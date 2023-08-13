using blizzardCrawler.shared;

namespace blizzardCrawler.db;

public class MatchInfo
{
    public int MatchInfoId { get; set; }
    public long MatchDateUnixTimestamp { get; set; }
    public Decision Decision { get; set; }
    public Region Region { get; set; }
    public int PlayerId { get; set; }
    public virtual Player Player { get; set; } = null!;
}
