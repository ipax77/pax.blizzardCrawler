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
builder.Services.AddScoped<ICrawlerHandler, CrawlerHandler>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var crawlerHandler = scope.ServiceProvider.GetRequiredService<ICrawlerHandler>();
    var apiOptions = scope.ServiceProvider.GetRequiredService<IOptions<BlizzardAPIOptions>>();
    var context = scope.ServiceProvider.GetRequiredService<BlContext>();

    var players = await context.Players
        .OrderByDescending(o => o.LatestMatchInfo)
        .Skip(4000)
        .Take(100)
        .Select(s => new PlayerEtagIndex()
        {
            ProfileId = s.ToonId,
            RegionId = s.RegionId,
            RealmId = s.RealmId,
            Etag = s.Etag,
            LatestMatchInfo = s.LatestMatchInfo,
        }).ToListAsync();

    List<MatchInfoResult> results = new();
    await foreach(var matchInfo in crawlerHandler.GetMatchInfos(players, apiOptions.Value))
    {
        results.Add(matchInfo);
    }
    var status = crawlerHandler.GetLatestCrawlerStatus();

    Console.WriteLine($"results: {results.Count}");

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

