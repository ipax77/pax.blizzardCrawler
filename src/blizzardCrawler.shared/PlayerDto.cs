namespace blizzardCrawler.shared;

public record PlayerDto
{
    public PlayerDto() { }
    public PlayerDto(int profileId, int regionId, int realmId)
    {
        ProfileId = profileId;
        RegionId = regionId;
        RealmId = realmId;
    }

    public int PlayerId { get; set; }
    public int ProfileId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
}

public record  PlayerIndex
{
    public PlayerIndex() { }
    public PlayerIndex(int profileId, int regionId, int realmId)
    {
        ProfileId = profileId;
        RegionId = regionId;
        RealmId = realmId;
    }

    public int ProfileId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
}

public record PlayerEtagIndex
{
    public PlayerEtagIndex() {  }
    public PlayerEtagIndex(PlayerIndex player, string? etag)
    {
        ProfileId = player.ProfileId;
        RegionId = player.RegionId;
        RealmId = player.RealmId;
        Etag = etag;
    }
    public int ProfileId { get; set; }
    public int RegionId { get; set; }
    public int RealmId { get; set; }
    /// <summary>
    /// Etag without W and quotes 
    /// </summary>
    public string? Etag { get; set; }
    public DateTime? LatestMatchInfo { get; set; }

    public PlayerIndex GetPlayerIndex()
    {
        return new(ProfileId, RegionId, RealmId);
    }
}