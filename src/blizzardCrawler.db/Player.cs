using System.ComponentModel.DataAnnotations;

namespace blizzardCrawler.db;

public class Player
{
    public Player()
    {
        MatchInfos = new HashSet<MatchInfo>();
    }

    public int PlayerId { get; set; }
    [MaxLength(30)]
    public string Name { get; set; } = null!;
    public int ToonId { get; set; }

    public int RegionId { get; set; }
    public int RealmId { get; set; }
    [MaxLength(40)]
    public string? Etag { get; set; }
    public virtual ICollection<MatchInfo> MatchInfos { get; set; }
}