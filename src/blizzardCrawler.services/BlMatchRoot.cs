namespace blizzardCrawler.services;

internal record BlMatchRoot
{
    public List<BlMatch> Matches { get; set; } = new();
}
