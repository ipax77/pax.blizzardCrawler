using blizzardCrawler.db;
using blizzardCrawler.services;
using blizzardCrawler.shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace blizzardCrawler.cli;

class Program
{
    static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: false)
            .AddJsonFile("/data/localserverconfig.json", optional: true, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();

        var serverVersion = new MySqlServerVersion(new Version(5, 7, 42));

        var connectionString = configuration["ServerConfig:BlConnectionString"] ?? "";

        services.AddOptions<BlizzardAPIOptions>()
            .Configure(x =>
            {
                x.ClientName = configuration["ServerConfig:BlizzardAPI:ClientName"] ?? "";
                x.ClientId = configuration["ServerConfig:BlizzardAPI:ClientId"] ?? "";
                x.ClientSecret = configuration["ServerConfig:BlizzardAPI:ClientSecret"] ?? "";
            });

        services.AddOptions<DbImportOptions>()
            .Configure(x =>
            {
                x.ImportConnectionString = configuration["ServerConfig:BlImportConnectionString"] ?? "";
                x.DsstatsConnectionString = configuration["ServerConfig:ImportConnectionString"] ?? "";
                x.ArcadeConnectionString = configuration["ServerConfig:DsstatsProdConnectionString"] ?? "";
                x.LogFile = configuration["ServerConfig:CrawlerLogFile"] ?? "log.txt";
                x.CrawlerThreadsCount = int.Parse(configuration["ServerConfig:CrawlerHttpThreadsCount"] ?? "30");
            });

        services.AddLogging(options =>
        {
            options.SetMinimumLevel(LogLevel.Debug);
            options.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            options.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
            options.AddConsole();
        });

        services.AddDbContext<BlContext>(options =>
        {
            options.UseMySql(connectionString, serverVersion, p =>
            {
                p.CommandTimeout(600);
                p.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
            });
        });

        services.AddHttpClient();

        services.AddMemoryCache();
        services.AddSingleton<PlayerRepository>();
        services.AddSingleton<MatchRepository>();
        services.AddSingleton<CrawlerService>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("indahouse");

        //var context = scope.ServiceProvider.GetRequiredService<BlContext>();
        //CheckDate(context);

        //var count = context.Players.Count();
        //logger.LogInformation("Replays: {count}", count);

        var crawlerService = scope.ServiceProvider.GetRequiredService<CrawlerService>();

        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent the application from immediately exiting
            cts.Cancel();     // Cancel the token source
            Console.WriteLine("CTRL-C detected. Cancelling the operation...");
        };

        crawlerService.StartJob(cts.Token);

        //crawlerService.GetPlayerMatchInfos(new()
        //{
        //    ToonId = 10188255,
        //    RegionId = 1,
        //    RealmId = 1
        //}, "90d-igkzL1g6rMQ3OO/0qho0au8+Bh8").GetAwaiter().GetResult();

        Console.WriteLine("Press Enter to exit.");
        Console.ReadLine();
        cts.Cancel();
    }

    private static void CheckDate(BlContext context)
    {
        var matchInfos = context.MatchInfos
            .Where(x => x.PlayerId == 5)
            .OrderByDescending(o => o.MatchDateUnixTimestamp)
            .Select(s => s.MatchDateUnixTimestamp)
            .ToList();

        Console.WriteLine(string.Join(Environment.NewLine, matchInfos.Select(s => DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime)));
    }
}
