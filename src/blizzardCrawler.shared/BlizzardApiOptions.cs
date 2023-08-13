namespace blizzardCrawler.shared;

public record BlizzardAPIOptions
{
    public string ClientName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}