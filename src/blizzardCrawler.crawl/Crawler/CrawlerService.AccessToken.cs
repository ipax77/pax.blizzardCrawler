using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace blizzardCrawler.crawl.Crawler;

public partial class CrawlerService
{
    internal async Task<TokenResponse?> GetAccessToken(bool forceRenew = false)
    {
        string memKey = "AccessToken" + apiOptions.ClientId;

        await ssToken.WaitAsync(cancellationToken);
        try
        {
            if (!memoryCache.TryGetValue(memKey, out TokenResponse? tokenResponse)
                || tokenResponse is null)
            {
                HttpResponseMessage response;
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.battle.net/token"))
                {
                    var authenticationString = $"{apiOptions.ClientId}:{apiOptions.ClientSecret}";
                    var base64String = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64String);
                    request.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });
                    request.Headers.Accept.Add(new("application/json"));
                    response = await httpClient.SendAsync(request, cancellationToken);
                }
                response.EnsureSuccessStatusCode();

                tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                if (tokenResponse is not null)
                {
                    memoryCache.Set(memKey, tokenResponse, TimeSpan.FromSeconds(tokenResponse.ExpiresIn - 60));
                    logger.LogInformation("Access token sucessfully received.");
                }
            }
            return tokenResponse;
        }
        catch (OperationCanceledException) { }
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