using blizzardCrawler.shared;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    [GeneratedRegex(@"""(.*?)""")]
    private static partial Regex EtagRegex();

    private async Task<MatchResponse> GetMatchHistory(PlayerEtagIndex player)
    {
        await ss.WaitAsync();
        try
        {
            if (tokenBucketSecond is null 
                || !await tokenBucketSecond.UseTokenAsync(cancellationToken))
            {
                return new() { StatusCode = 777 };
            }

            if (tokenBucketHour is null 
                || !await tokenBucketHour.UseTokenAsync(cancellationToken))
            {
                return new() { StatusCode = 778 };
            }

            var token = await GetAccessToken();
            ArgumentNullException.ThrowIfNull(token);

            string? region = GetRegionString(player);
            if (region == null)
            {
                return new() { StatusCode = 766 };
            }

            HttpResponseMessage response;
            using (var cts = new CancellationTokenSource(_requestTimeout))
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, $"https://{region}.blizzard.com/sc2/legacy/profile/{player.RegionId}/{player.RealmId}/{player.ProfileId}/matches"))
                {
                    request.Headers.Authorization = new("Bearer", token.AccessToken);
                    request.Headers.Accept.Add(new("application/json"));
                    if (!string.IsNullOrEmpty(player.Etag))
                    {
                        EntityTagHeaderValue etagValue = new($"\"{player.Etag}\"", true);
                        request.Headers.IfNoneMatch.Add(etagValue);
                    }
                    response = await httpClient.SendAsync(request, cts.Token);
                }
            }

            if (response.IsSuccessStatusCode)
            {
                var matchRoot = await response.Content.ReadFromJsonAsync<BlMatchRoot>(cancellationToken);
                if (matchRoot is not null)
                {
                    var responseEtag = response.Headers.GetValues("ETag").FirstOrDefault();
                    return new()
                    {
                        StatusCode = 200,
                        Matches = matchRoot.Matches,
                        Etag = ExtractEtag(responseEtag)
                    };
                }
                else
                {
                    return new() { StatusCode = 798 };
                }
            }
            else
            {
                return new() { StatusCode = (int)response.StatusCode };
            }
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

    private string? GetRegionString(PlayerEtagIndex player)
    {
        return (player.RegionId, player.RealmId) switch
        {
            (1, _) => "us.api",
            (2, _) => "eu.api",
            (3, 1) => "kr.api",
            (3, 2) => "tw.api",
            (5, _) => "gateway",
            _ => null
        };
    }

    private static string? ExtractEtag(string? etagString)
    {
        if (string.IsNullOrEmpty(etagString))
        {
            return null;
        }

        Match match = EtagRegex().Match(etagString);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }
        return null;
    }

    private async Task<MatchResponse> MockGetMatchHistory(PlayerEtagIndex player)
    {
        List<int> possibleReturnCodes = new() { 200, 200, 200, 766, 404, 701, 504, 503, 304 };

        await ss.WaitAsync();
        try
        {
            if (tokenBucketSecond is null
                || !await tokenBucketSecond.UseTokenAsync(cancellationToken))
            {
                return new() { StatusCode = 777 };
            }

            if (tokenBucketHour is null
                || !await tokenBucketHour.UseTokenAsync(cancellationToken))
            {
                return new() { StatusCode = 778 };
            }
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
