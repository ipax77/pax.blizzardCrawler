using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace blizzardCrawler.services;

public partial class CrawlerService
{
    private async Task<TokenResponse?> GetAccessToken()
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
                    euHttpClient.DefaultRequestHeaders.Remove("Authorization");
                    euHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.AccessToken}");
                    naHttpClient.DefaultRequestHeaders.Remove("Authorization");
                    naHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.AccessToken}");

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
}
