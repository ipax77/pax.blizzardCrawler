using blizzardCrawler.shared;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace blizzardCrawler.services;

public partial class CrawlerService
{
    private static string mapName = "Direct Strike";

    private async Task<(List<BlMatch>, int, string?)> GetMatchHistory(PlayerIndex player, string? etag, bool dsOnly = true, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await tokenBucketSeconds.UseTokenAsync(cancellationToken))
            {
                return (new(), 777, null);
            }

            if (!await tokenBucketHour.UseTokenAsync(cancellationToken))
            {
                return (new(), 778, null);
            }

            var token = await GetAccessToken();
            ArgumentNullException.ThrowIfNull(token);

            var httpClient = player.RegionId switch
            {
                1 => naHttpClient,
                2 => euHttpClient,
                _ => null
            };

            if (httpClient == null)
            {
                return (new(), 766, null);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"/sc2/legacy/profile/{player.RegionId}/{player.RealmId}/{player.ToonId}/matches");
            if (!string.IsNullOrEmpty(etag))
            {
                request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            //var response = 
            //    await httpClient.GetAsync($"/sc2/legacy/profile/{player.RegionId}/{player.RealmId}/{player.ToonId}/matches",
            //        cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var matchRoot = await response.Content.ReadFromJsonAsync<BlMatchRoot>(cancellationToken);
                if (matchRoot is not null)
                {
                    var responseEtag = response.Headers.GetValues("ETag").FirstOrDefault();
                    if (dsOnly)
                    {
                        return (matchRoot.Matches.Where(x => x.Map.Equals(mapName, StringComparison.Ordinal)).ToList(), 200, responseEtag);
                    }
                    else
                    {
                        return (matchRoot.Matches, 200, responseEtag);
                    }
                }
                else
                {
                    return (new(), 798, null);
                }
            }
            else
            {
                return (new(), (int)response.StatusCode, null);
            }

        }
        catch (Exception ex)
        {
            logger.LogError("failed getting matchinfo: {error}", ex.Message);
            return (new(), 799, null);
        }
    }
}
