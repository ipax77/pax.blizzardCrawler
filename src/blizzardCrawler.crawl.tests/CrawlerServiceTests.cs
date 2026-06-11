using System.Collections.Concurrent;
using System.Net;
using System.Text;
using blizzardCrawler.crawl;
using blizzardCrawler.crawl.Crawler;
using blizzardCrawler.shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace blizzardCrawler.crawl.tests;

[TestClass]
public sealed class CrawlerServiceTests
{
    [TestMethod]
    public async Task StartJob_SendsAuthHeadersAndCachesTokenForMultiplePlayers()
    {
        var handler = new FakeBlizzardApiHandler();
        handler.EnqueueToken("access-token");
        handler.EnqueueMatchHistory(HttpStatusCode.OK, "etag-new", 1_700_000_000);
        handler.EnqueueMatchHistory(HttpStatusCode.OK, "etag-second", 1_700_000_100);
        var service = CreateService(handler);

        var players = new List<PlayerEtagIndex>
        {
            new() { ProfileId = 10, RegionId = 2, RealmId = 1, Etag = "etag-old" },
            new() { ProfileId = 11, RegionId = 2, RealmId = 1 }
        };

        var results = await RunJob(service, players);

        var tokenRequest = AssertSingle(handler.Requests.Where(r => r.Method == HttpMethod.Post));
        Assert.AreEqual("Basic", tokenRequest.AuthorizationScheme);
        Assert.AreEqual(Convert.ToBase64String(Encoding.ASCII.GetBytes("client:secret")), tokenRequest.AuthorizationParameter);
        StringAssert.Contains(tokenRequest.Content ?? string.Empty, "grant_type=client_credentials");

        var matchRequests = handler.Requests.Where(r => r.Method == HttpMethod.Get).ToList();
        Assert.HasCount(2, matchRequests);
        Assert.IsTrue(matchRequests.All(r => r.AuthorizationScheme == "Bearer" && r.AuthorizationParameter == "access-token"));
        Assert.IsTrue(matchRequests.Any(r => r.IfNoneMatch.Contains("W/\"etag-old\"")));

        var first = results.Single(r => r.Player.ProfileId == 10);
        Assert.AreEqual(200, first.StatusCode);
        Assert.AreEqual("etag-new", first.Player.Etag);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).UtcDateTime, first.Player.LatestMatchInfo);
        Assert.HasCount(1, first.MatchInfos);

        var mapped = new MatchInfoResult(first);
        Assert.AreEqual(Decision.Win, mapped.MatchInfos[0].Decision);
        Assert.AreEqual(Speed.Faster, mapped.MatchInfos[0].Speed);
        Assert.AreEqual(Region.Eu, mapped.MatchInfos[0].Region);
        Assert.AreEqual("Golden Wall", mapped.MatchInfos[0].Map);
    }

    [TestMethod]
    public async Task StartJob_ReturnsStatusCodesForNotModifiedAndNotFound()
    {
        var handler = new FakeBlizzardApiHandler();
        handler.EnqueueToken("access-token");
        handler.EnqueueStatus(HttpStatusCode.NotModified);
        handler.EnqueueStatus(HttpStatusCode.NotFound);
        var service = CreateService(handler);

        var players = new List<PlayerEtagIndex>
        {
            new() { ProfileId = 20, RegionId = 2, RealmId = 1 },
            new() { ProfileId = 21, RegionId = 2, RealmId = 1 }
        };

        var results = await RunJob(service, players);

        CollectionAssert.AreEquivalent(new[] { 304, 404 }, results.Select(r => r.StatusCode).ToArray());
        Assert.IsTrue(results.All(r => r.MatchInfos.Count == 0));
    }

    [TestMethod]
    public async Task StartJob_RetriesRetryableResponsesUntilSuccess()
    {
        var handler = new FakeBlizzardApiHandler();
        handler.EnqueueToken("access-token");
        handler.EnqueueStatus(HttpStatusCode.ServiceUnavailable);
        handler.EnqueueStatus(HttpStatusCode.GatewayTimeout);
        handler.EnqueueStatus((HttpStatusCode)429);
        handler.EnqueueMatchHistory(HttpStatusCode.OK, "etag-final", 1_700_000_200);
        var service = CreateService(handler);

        var results = await RunJob(
            service,
            new List<PlayerEtagIndex> { new() { ProfileId = 30, RegionId = 2, RealmId = 1 } },
            timeout: TimeSpan.FromSeconds(6));

        var result = AssertSingle(results);
        Assert.AreEqual(200, result.StatusCode);
        Assert.AreEqual("etag-final", result.Player.Etag);
        Assert.AreEqual(4, handler.Requests.Count(r => r.Method == HttpMethod.Get));
        Assert.AreEqual(1, handler.Requests.Count(r => r.Method == HttpMethod.Post));
    }

    [TestMethod]
    public async Task StartJob_InvalidRegionReturns766WithoutHttpCalls()
    {
        var handler = new FakeBlizzardApiHandler();
        var service = CreateService(handler);

        var results = await RunJob(service, new List<PlayerEtagIndex>
        {
            new() { ProfileId = 40, RegionId = 9, RealmId = 1 }
        });

        var result = AssertSingle(results);
        Assert.AreEqual(766, result.StatusCode);
        Assert.IsFalse(handler.Requests.Any(), "Invalid regions should not call the fake Blizzard API.");
    }

    [TestMethod]
    public async Task CrawlerHandler_GetMatchInfos_CompletesWithFakeApi()
    {
        var handler = new FakeBlizzardApiHandler();
        handler.EnqueueToken("access-token");
        handler.EnqueueMatchHistory(HttpStatusCode.OK, "etag-handler", 1_700_000_300);

        using var services = new ServiceCollection()
            .AddMemoryCache()
            .AddTransient<ICrawlerService>(sp => new CrawlerService(
                sp.GetRequiredService<IMemoryCache>(),
                NullLogger<CrawlerService>.Instance,
                new HttpClient(handler)))
            .AddScoped<ICrawlerHandler, CrawlerHandler>()
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var crawlerHandler = scope.ServiceProvider.GetRequiredService<ICrawlerHandler>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var results = new List<MatchInfoResult>();

        await foreach (var result in crawlerHandler.GetMatchInfos(
            new List<PlayerEtagIndex> { new() { ProfileId = 50, RegionId = 2, RealmId = 1 } },
            CreateOptions(),
            token: cts.Token))
        {
            results.Add(result);
        }

        var matchInfo = AssertSingle(results);
        Assert.AreEqual(200, matchInfo.StatusCode);
        Assert.AreEqual("etag-handler", matchInfo.Etag);
        Assert.HasCount(1, matchInfo.MatchInfos);
    }

    private static CrawlerService CreateService(FakeBlizzardApiHandler handler)
    {
        return new CrawlerService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CrawlerService>.Instance,
            new HttpClient(handler));
    }

    private static BlizzardAPIOptions CreateOptions()
    {
        return new BlizzardAPIOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            CrawlerThreadsCount = 1,
            MaxRequestsPerSecond = 100,
            MaxRequestsPerHour = 100,
            HttpRequestTimeoutInSeconds = 2,
            OAuthTokenEndpoint = "https://mock.blizzard.test/oauth/token",
            ApiBaseUrlFormat = "https://mock.blizzard.test/{0}",
            TooManyRequestsDelayInSeconds = 0
        };
    }

    private static async Task<List<MatchInfoEventArgs>> RunJob(
        CrawlerService service,
        List<PlayerEtagIndex> players,
        TimeSpan? timeout = null)
    {
        var results = new ConcurrentQueue<MatchInfoEventArgs>();
        var jobDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        service.MatchInfoReady += (_, result) => results.Enqueue(result);
        service.JobDone += (_, _) => jobDone.TrySetResult();

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        using var registration = cts.Token.Register(() => jobDone.TrySetCanceled(cts.Token));
        using var tbSecond = new TokenBucket(100, 1000, 1);
        using var tbHour = new TokenBucket(100, 3600000, 1);

        service.StartJob(players, CreateOptions(), tbSecond, tbHour, cts.Token);
        await jobDone.Task;

        return results.ToList();
    }

    private static T AssertSingle<T>(IEnumerable<T> values)
    {
        var list = values.ToList();
        Assert.HasCount(1, list);
        return list[0];
    }

    private sealed class FakeBlizzardApiHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new();

        public ConcurrentQueue<RequestSnapshot> Requests { get; } = new();

        public void EnqueueToken(string accessToken)
        {
            Enqueue(_ => JsonResponse(HttpStatusCode.OK, $$"""{"access_token":"{{accessToken}}","expires_in":3600}"""));
        }

        public void EnqueueMatchHistory(HttpStatusCode statusCode, string etag, long date)
        {
            Enqueue(_ =>
            {
                var response = JsonResponse(statusCode, $$"""{"matches":[{"map":"Golden Wall","type":"1v1","decision":"Win","speed":"Faster","date":{{date}}}]}""");
                response.Headers.TryAddWithoutValidation("ETag", $"W/\"{etag}\"");
                return response;
            });
        }

        public void EnqueueStatus(HttpStatusCode statusCode)
        {
            Enqueue(_ => new HttpResponseMessage(statusCode));
        }

        private void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            responses.Enqueue(response);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Enqueue(new RequestSnapshot(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.IfNoneMatch.Select(h => h.ToString()).ToArray(),
                content));

            if (responses.Count == 0)
            {
                throw new InvalidOperationException($"No fake response was queued for {request.Method} {request.RequestUri}.");
            }

            return responses.Dequeue()(request);
        }

        private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri? RequestUri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        IReadOnlyList<string> IfNoneMatch,
        string? Content);
}
