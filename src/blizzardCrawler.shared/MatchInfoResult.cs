namespace blizzardCrawler.shared;

public class MatchInfoEventArgs : EventArgs
{
    public PlayerEtagIndex Player { get; set; } = new();
    public List<BlMatch> MatchInfos { get; set; } = new();
    public int StatusCode { get; set; }
}

public record BlMatchRoot
{
    public List<BlMatch> Matches { get; set; } = new();
}

public record MatchResponse
{
    public int StatusCode { get; set; }
    public List<BlMatch> Matches { get; set; } = new();
    public string? Etag { get; set; }
}

public record BlMatch
{
    public string Map { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public long Date { get; set; }
}

public record MatchInfoResult
{
    public MatchInfoResult() { }
    public MatchInfoResult(MatchInfoEventArgs e)
    {
        Player = new(e.Player.ProfileId, e.Player.RegionId, e.Player.RealmId);
        Etag = e.Player.Etag;
        StatusCode = e.StatusCode;
        MatchInfos = e.MatchInfos.Select(s => new MatchDto()
        {
            Map = s.Map,
            MatchDateUnixTimestamp = s.Date,
            Decision = EnumExtensions.GetDecision(s.Decision),
            Speed = EnumExtensions.GetSpeed(s.Speed),
            Region = EnumExtensions.GetRegion(e.Player.RegionId),
        }).ToList();
    }

    public PlayerIndex Player { get; set; } = new();
    public List<MatchDto> MatchInfos { get; set; } = new();
    public string? Etag { get; set; }
    public int StatusCode { get; set; }

}