namespace blizzardCrawler.shared;

public class MatchInfoEventArgs : EventArgs
{
    public PlayerEtagIndex Player { get; set; } = new();
    public List<MatchDto> MatchInfos { get; set; } = new();
    public int StatusCode { get; set; }
}
