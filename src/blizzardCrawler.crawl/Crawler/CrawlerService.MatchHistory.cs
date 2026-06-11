using blizzardCrawler.shared;
using Microsoft.Extensions.Logging;
using System.Globalization;
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

            string? region = GetRegionString(player);
            if (region == null)
            {
                return new() { StatusCode = 766 };
            }

            var token = await GetAccessToken();
            ArgumentNullException.ThrowIfNull(token);

            HttpResponseMessage response;
            using (var cts = new CancellationTokenSource(_requestTimeout))
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, GetMatchHistoryUri(region, player)))
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

    private Uri GetMatchHistoryUri(string region, PlayerEtagIndex player)
    {
        var baseUrlFormat = string.IsNullOrWhiteSpace(apiOptions.ApiBaseUrlFormat)
            ? "https://{0}.blizzard.com"
            : apiOptions.ApiBaseUrlFormat.TrimEnd('/');

        var baseUrl = baseUrlFormat.Contains("{0}", StringComparison.Ordinal)
            ? string.Format(CultureInfo.InvariantCulture, baseUrlFormat, region)
            : $"{baseUrlFormat}/{region}";

        return new Uri($"{baseUrl.TrimEnd('/')}/sc2/legacy/profile/{player.RegionId}/{player.RealmId}/{player.ProfileId}/matches");
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
}
