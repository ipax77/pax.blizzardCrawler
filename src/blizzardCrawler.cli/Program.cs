using blizzardCrawler.crawl.Crawler;
using blizzardCrawler.crawl;
using blizzardCrawler.shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace blizzardCrawler.cli;

class Program
{
    public static ServiceProvider? serviceProvider;

    static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: false)
            .AddJsonFile("/data/localserverconfig.json", optional: true, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();

        services.AddOptions<BlizzardAPIOptions>()
            .Configure(x =>
            {
                x.ClientName = configuration["ServerConfig:BlizzardAPI:ClientName"] ?? "";
                x.ClientId = configuration["ServerConfig:BlizzardAPI:ClientId"] ?? "";
                x.ClientSecret = configuration["ServerConfig:BlizzardAPI:ClientSecret"] ?? "";
                x.CrawlerThreadsCount = int.Parse(configuration["ServerConfig:CrawlerHttpThreadsCount"] ?? "30");
                x.MaxRequestsPerSecond = int.Parse(configuration["ServerConfig:MaxRequestsPerSecond"] ?? "100");
                x.MaxRequestsPerHour = int.Parse(configuration["ServerConfig:MaxRequestsPerHour"] ?? "36000");
                x.HttpRequestTimeoutInSeconds = int.Parse(configuration["ServerConfig:HttpRequestTimeoutInSeconds"] ?? "10");
            });

        services.AddLogging(options =>
        {
            options.SetMinimumLevel(LogLevel.Information);
            options.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            options.AddConsole();
        });

        services.AddMemoryCache();

        services.AddTransient<ICrawlerService, CrawlerService>();
        services.AddScoped<ICrawlerHandler, CrawlerHandler>();

        serviceProvider = services.BuildServiceProvider();

        Test().GetAwaiter().GetResult();
    }

    public static async Task Test()
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        List<PlayerEtagIndex> players = new()
        {
            new PlayerEtagIndex()
            {
                ProfileId = 180933, RegionId = 2, RealmId = 1, Etag = "9a0-deYOqqlQUsDwKXD5F53d/OtIFLU"
            },
            new PlayerEtagIndex()
            {
                ProfileId = 10096799, RegionId = 2, RealmId = 1, Etag = "97b-nFa++8z3dwbU+QKvYXU01au/Uto"
            },
            new PlayerEtagIndex()
            {
                ProfileId = 3155130, RegionId = 2, RealmId = 1, Etag = "905-1DbQwwpQ0ip67NVBM9mhwWBRY+g"
            },
            new PlayerEtagIndex()
            {
                ProfileId = 1328487, RegionId = 2, RealmId = 1, Etag = "984-kXrQGWE9VoAVvg5HXlVolQzE6yk"
            },
            new PlayerEtagIndex()
            {
                ProfileId = 865346, RegionId = 2, RealmId = 1, Etag = "92e-oeeVu4LTgFR+uiqyrF70we2JXy0"
            },
            new PlayerEtagIndex()
            {
                ProfileId = 977214, RegionId = 2, RealmId = 1, Etag = null
            },
        };

        using var scope = serviceProvider.CreateScope();
        var crawlHandler = scope.ServiceProvider.GetRequiredService<ICrawlerHandler>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BlizzardAPIOptions>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        List<MatchInfoResult> results = new();

        await foreach(var matchInfo in crawlHandler.GetMatchInfos(players, options.Value))
        {
            results.Add(matchInfo);
        }
        var status = crawlHandler.GetLatestCrawlerStatus();


        StringBuilder sb = new();
        sb.AppendLine("Results:");
        foreach(var matchInfo in results)
        {
            sb.AppendLine($"ProfileId: {matchInfo.Player.ProfileId}, StatusCode: {matchInfo.StatusCode}");
            foreach (var match in matchInfo.MatchInfos)
            {
                sb.AppendLine($"\t{match.Map} - {DateTimeOffset.FromUnixTimeSeconds(match.MatchDateUnixTimestamp).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")}");
            }
        }
        logger.LogInformation("{restuls}", sb.ToString());
        logger.LogInformation("{status}", status);
    }
}
