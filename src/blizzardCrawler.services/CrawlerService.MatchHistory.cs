using blizzardCrawler.db;
using blizzardCrawler.shared;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace blizzardCrawler.services;

public partial class CrawlerService
{
    private static readonly string mapName = "Direct Strike";
    private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(10);

    private async Task<(List<BlMatch>, int, string?)> GetMatchHistory(PlayerIndex player,
                                                                      string? etag,
                                                                      bool re,
                                                                      bool dsOnly = true,
                                                                      CancellationToken cancellationToken = default)
    {
        await ss.WaitAsync(cancellationToken);
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

            var token = await GetAccessToken(cancellationToken);
            ArgumentNullException.ThrowIfNull(token);

            string? region = player.RegionId switch
            {
                1 => "us",
                2 => "eu",
                _ => null
            };

            if (region == null)
            {
                return (new(), 766, null);
            }

            HttpResponseMessage response;
            using (var cts = new CancellationTokenSource(_requestTimeout))
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, $"https://{region}.api.blizzard.com/sc2/legacy/profile/{player.RegionId}/{player.RealmId}/{player.ToonId}/matches"))
                {
                    request.Headers.Authorization = new("Bearer", token.AccessToken);
                    request.Headers.Accept.Add(new("application/json"));
                    if (!string.IsNullOrEmpty(etag))
                    {
                        EntityTagHeaderValue etagValue = new($"\"{etag}\"", true);
                        request.Headers.IfNoneMatch.Add(etagValue);
                    }
                    response = await httpClient.SendAsync(request, cts.Token);
                }
            }

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
        catch (OperationCanceledException)
        {
            return (new(), 701, null);
        }
        catch (Exception ex)
        {
             logger.LogError("failed getting matchinfo: {error}", ex.Message);
            return (new(), 799, null);
        }
        finally
        {
            ss.Release();
        }
    }
}
