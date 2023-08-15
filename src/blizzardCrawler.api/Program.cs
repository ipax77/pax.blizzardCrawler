using blizzardCrawler.crawl.Crawler;
using blizzardCrawler.db;
using blizzardCrawler.shared;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddSingleton<CrawlerService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
CancellationTokenSource cts = new();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var crawlerService = scope.ServiceProvider.GetRequiredService<CrawlerService>();

    List<PlayerEtagIndex> players = new List<PlayerEtagIndex>();
    for (int i = 0; i < 10000; i++)
    {
        players.Add(new() { ToonId = 1, RegionId = 1, RealmId = 1, Etag = null, LatestMatchInfo = null });
    }
    crawlerService.StartJob(players, cts.Token);

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
