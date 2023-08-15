using blizzardCrawler.crawl;
using blizzardCrawler.crawl.Crawler;
using blizzardCrawler.db;
using blizzardCrawler.shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

int j = 0;
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("/data/localserverconfig.json", optional: true, reloadOnChange: false);

// Add services to the container.
builder.Services.AddOptions<BlizzardAPIOptions>()
    .Configure(x =>
    {
        x.ClientName = builder.Configuration["ServerConfig:BlizzardAPI:ClientName"] ?? "";
        x.ClientId = builder.Configuration["ServerConfig:BlizzardAPI:ClientId"] ?? "";
        x.ClientSecret = builder.Configuration["ServerConfig:BlizzardAPI:ClientSecret"] ?? "";
        x.LogFile = builder.Configuration["ServerConfig:CrawlerLogFile"] ?? "log.txt";
        x.CrawlerThreadsCount = int.Parse(builder.Configuration["ServerConfig:CrawlerHttpThreadsCount"] ?? "30");
        x.MaxRequestsPerSecond = int.Parse(builder.Configuration["ServerConfig:MaxRequestsPerSecond"] ?? "100");
        x.MaxRequestsPerHour = int.Parse(builder.Configuration["ServerConfig:MaxRequestsPerHour"] ?? "36000");
        x.HttpRequestTimeoutInSeconds = int.Parse(builder.Configuration["ServerConfig:HttpRequestTimeoutInSeconds"] ?? "10");
    });

builder.Services.AddOptions<DbImportOptions>()
    .Configure(x =>
    {
        x.ImportConnectionString = builder.Configuration["ServerConfig:BlConnectionString"] ?? "";
        x.LogFile = builder.Configuration["ServerConfig:CrawlerLogFile"] ?? "log.txt";
        x.CrawlerThreadsCount = int.Parse(builder.Configuration["ServerConfig:CrawlerHttpThreadsCount"] ?? "30");
    });

var serverVersion = new MySqlServerVersion(new Version(5, 7, 42));
var connectionString = builder.Configuration["ServerConfig:BlConnectionString"] ?? "";
builder.Services.AddDbContext<BlContext>(options =>
{
    options.UseMySql(connectionString, serverVersion, p =>
    {
        p.CommandTimeout(600);
        p.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    });
});

builder.Services.AddMemoryCache();

builder.Services.AddTransient<ICrawlerService, CrawlerService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
CancellationTokenSource cts = new();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var crawlerService1 = scope.ServiceProvider.GetRequiredService<ICrawlerService>();
    var crawlerService2 = scope.ServiceProvider.GetRequiredService<ICrawlerService>();
    var apiOptions = scope.ServiceProvider.GetRequiredService<IOptions<BlizzardAPIOptions>>();

    List<PlayerEtagIndex> players = new List<PlayerEtagIndex>();
    for (int i = 0; i < 10000; i++)
    {
        players.Add(new() { ToonId = 1, RegionId = 1, RealmId = 1, Etag = null, LatestMatchInfo = null });
    }
    TokenBucket tbSecond = new(apiOptions.Value.MaxRequestsPerSecond, 1000, 3000);
    TokenBucket tbHour = new(apiOptions.Value.MaxRequestsPerHour, 3600000, 60000);

    crawlerService1.MatchInfoReady += HandleIt;
    crawlerService2.MatchInfoReady += HandleIt;

    crawlerService1.StartJob(players.Take(1000).ToList(), apiOptions.Value, tbSecond, tbHour, cts.Token);
    crawlerService2.StartJob(players.Skip(2000).Take(2000).ToList(), apiOptions.Value, tbSecond, tbHour, cts.Token);

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

void HandleIt(object? sender, MatchInfoEventArgs e)
{
    Interlocked.Increment(ref j);
    if (j % 100 == 0)
    {
        Console.WriteLine($"j: {j}");
    }
}