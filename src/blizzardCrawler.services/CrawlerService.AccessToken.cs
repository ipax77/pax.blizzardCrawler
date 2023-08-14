using blizzardCrawler.db;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;

namespace blizzardCrawler.services;

public partial class CrawlerService
{
    private async Task<TokenResponse?> GetAccessToken(CancellationToken cancellationToken)
    {
        string memKey = "AccessToken";

        await ssToken.WaitAsync();
        try
        {
            if (!memoryCache.TryGetValue(memKey, out TokenResponse? token)
                || token is null)
            {
                HttpResponseMessage response;
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.battle.net/token"))
                {
                    var authenticationString = $"{options.Value.ClientId}:{options.Value.ClientSecret}";
                    var base64String = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64String);
                    request.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });
                    request.Headers.Accept.Add(new("application/json"));
                    response = await httpClient.SendAsync(request, cancellationToken);
                }
                response.EnsureSuccessStatusCode();

                token = await response.Content.ReadFromJsonAsync<TokenResponse>();
                if (token is not null)
                {
                    // SetAccessToken(token.AccessToken);
                    memoryCache.Set(memKey, token, TimeSpan.FromSeconds(token.ExpiresIn - 2));
                    await Task.Delay(2000); // wait for Blizzard to prozcess the new token
                    logger.LogInformation("Access token sucessfully received.");
                }
            }
            return token;
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

    private async Task<TokenResponse?> GetAccessToken_deprecated()
    {
        string memKey = "AccessToken";

        await ssToken.WaitAsync();
        try
        {
            if (!memoryCache.TryGetValue(memKey, out TokenResponse? token)
                || token is null)
            {

                var httpAuthClient = httpClientFactory.CreateClient();
                httpAuthClient.BaseAddress = new Uri("https://oauth.battle.net");
                httpAuthClient.DefaultRequestHeaders.Add("Accept", "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, "/token");

                var authenticationString = $"{options.Value.ClientId}:{options.Value.ClientSecret}";
                var base64String = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64String);
                request.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

                var response = await httpAuthClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                token = await response.Content.ReadFromJsonAsync<TokenResponse>();
                if (token is not null)
                {
                    // SetAccessToken(token.AccessToken);
                    memoryCache.Set(memKey, token, TimeSpan.FromSeconds(token.ExpiresIn - 2));
                    await Task.Delay(2000); // wait for Blizzard to prozcess the new token
                }
            }
            return token;
        }
        finally
        {
            ssToken.Release();
        }
    }

    private void SetAccessToken(string accessToken)
    {
        //euHttpClient.DefaultRequestHeaders.Remove("Authorization");
        //euHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        //naHttpClient.DefaultRequestHeaders.Remove("Authorization");
        //naHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
    }
}
