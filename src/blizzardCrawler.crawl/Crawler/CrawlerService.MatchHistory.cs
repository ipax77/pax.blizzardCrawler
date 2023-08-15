using blizzardCrawler.shared;
using Microsoft.Extensions.Logging;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    private async Task<MatchResponse> GetMatchHistory(PlayerEtagIndex player)
    {
        List<int> possibleReturnCodes = new() { 200, 200, 200, 766, 404, 701, 429, 504, 503, 304 };

        await ss.WaitAsync();
        try
        {
            if (!await tokenBucketSeconds.UseTokenAsync(cancellationToken))
            {
                return new() { StatusCode = 777 };
            }

            if (!await tokenBucketHour.UseTokenAsync(cancellationToken))
            {
                return new() { StatusCode = 778 };
            }

            var token = await GetAccessToken();
            ArgumentNullException.ThrowIfNull(token);
            
            int index = Random.Shared.Next(0, possibleReturnCodes.Count);
            await Task.Delay(index + 1 * 1);
            return new() { StatusCode = possibleReturnCodes[index] };
        }
        catch (OperationCanceledException)
        {
            return new() { StatusCode = 701 };
        }
        catch (Exception ex)
        {
            logger.LogError("player failed: {error}", ex.Message);
            return new() { StatusCode = 799 };
        }
        finally
        {
            ss.Release();
        }
    }
}


internal record MatchResponse
{
    public int StatusCode { get; set; }
    public List<BlMatch> Matches { get; set; } = new();
    public string? Etag { get; set; }
}

internal record BlMatch
{
    public string Map { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public long Date { get; set; }
}