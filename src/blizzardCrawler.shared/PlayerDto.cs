namespace blizzardCrawler.shared;

public record PlayerDto
{
    public PlayerDto() { }
    public PlayerDto(int toonId, int regionId, int realmId)
    {
        ToonId = toonId;
        RegionId = regionId;
        RealmId = realmId;
    }

    public int PlayerId { get; set; }
    public int ToonId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
}

public record  PlayerIndex
{
    public PlayerIndex() { }
    public PlayerIndex(int toonId, int regionId, int realmId)
    {
        ToonId = toonId;
        RegionId = regionId;
        RealmId = realmId;
    }

    public int ToonId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
}

public record PlayerEtagIndex
{
    public PlayerEtagIndex() {  }
    public PlayerEtagIndex(PlayerIndex player, string? etag)
    {
        ToonId = player.ToonId;
        RegionId = player.RegionId;
        RealmId = player.RealmId;
        Etag = etag;
    }
    public int ToonId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
    /// <summary>
    /// Etag without W and quotes 
    /// </summary>
    public string? Etag { get; set; }
    public DateTime? LatestMatchInfo { get; set; }

    public PlayerIndex GetPlayerIndex()
    {
        return new(ToonId, RegionId, RealmId);
    }
}