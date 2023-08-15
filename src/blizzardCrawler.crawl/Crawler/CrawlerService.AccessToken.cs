using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    internal async Task<TokenResponse?> GetAccessToken()
    {
        string memKey = "AccessToken" + apiOptions.ClientId;

        await ssToken.WaitAsync();
        try
        {
            if (!memoryCache.TryGetValue(memKey, out TokenResponse? tokenResponse)
                || tokenResponse is null)
            {
                await Task.Delay(2000);
                tokenResponse = new()
                {
                    AccessToken = "test",
                    ExpiresIn = 100000
                };
                memoryCache.Set(memKey, tokenResponse, TimeSpan.FromSeconds(tokenResponse.ExpiresIn));
                logger.LogInformation("Access token sucessfully received.");
            }
            return tokenResponse;
        }
        catch (Exception ex)
        {
            logger.LogError("failed getting access token: {error}", ex.Message);
        }
        finally
        {
            ssToken.Release();
        }
        return null;
    }
}

internal record TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}