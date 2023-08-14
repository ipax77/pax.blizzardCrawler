using blizzardCrawler.db;
using blizzardCrawler.shared;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace blizzardCrawler.services;

public partial class PlayerRepository
{
    private readonly IOptions<DbImportOptions> importOptions;
    private readonly IServiceScopeFactory scopeFactory;

    private readonly object lockobject = new();

    public PlayerRepository(IOptions<DbImportOptions> importOptions, IServiceScopeFactory scopeFactory)
    {
        this.importOptions = importOptions;
        this.scopeFactory = scopeFactory;
    }

    private ConcurrentDictionary<PlayerIndex, PlayerCrawlInfo> playersStore = new();
    private static readonly Regex etagRegex = EtagRegex();

    [GeneratedRegex(@"""(.*?)""")]
    private static partial Regex EtagRegex();

    public string? GetPlayerEtag(PlayerIndex player)
    {
        if (playersStore.TryGetValue(player, out PlayerCrawlInfo? info)
            && info is not null)
        {
            return info.Etag;
        }
        return null;
    }

    public string GetPlayerInsertEtag(PlayerIndex player)
    {
        if (playersStore.TryGetValue(player, out PlayerCrawlInfo? info)
            && info is not null)
        {
            return $"'{info.Etag}'";
        }
        return "NULL";
    }

    public int GetPlayer503s(PlayerIndex player)
    {
        if (playersStore.TryGetValue(player, out PlayerCrawlInfo? info)
            && info is not null)
        {
            if (info.CrawlStatusCodes.Count > 0)
            {
                return info.CrawlStatusCodes
                    .Count(c => c == 503);
            }
        }
        return -1;
    }

    public async Task<Dictionary<PlayerIndex, int>> StorePlayers(List<PlayerIndex> players)
    {
        using var connection = new MySqlConnection(importOptions.Value.ImportConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        var command = connection.CreateCommand();
        command.Transaction = transaction;

        int batchSize = 1000;

        int numberOfBatches = (int)Math.Ceiling((double)players.Count / batchSize);

        for (int i = 0; i < numberOfBatches; i++)
        {
            var currentBatch = players.Skip(i * batchSize).Take(batchSize);
            var insertStatement = new StringBuilder();
            insertStatement.AppendLine($"INSERT IGNORE INTO {nameof(BlContext.Players)} ({nameof(Player.Name)}, {nameof(Player.ToonId)}, {nameof(Player.RegionId)}, {nameof(Player.RealmId)}, {nameof(Player.Etag)}) VALUES");
            insertStatement.Append(string.Join($",{Environment.NewLine}", currentBatch.Select(s => $"('',{s.ToonId},{s.RegionId},{s.RealmId},{GetPlayerInsertEtag(s)})")));
            command.CommandText = insertStatement.ToString();
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BlContext>();

        var playerIds = (await context.Players
            .Select(s => new
            {
                s.PlayerId,
                s.ToonId,
                s.RegionId,
                s.RealmId
            })
            .ToListAsync())
            .ToDictionary(k => new PlayerIndex(k.ToonId, k.RegionId, k.RealmId), v => v.PlayerId);

        foreach (var ent in playersStore)
        {
            if (playerIds.TryGetValue(ent.Key, out var playerId))
            {
                ent.Value.PlayerId = playerId;
            }
        }

        return playerIds;
    }

    public void SetCrawlInfo(PlayerIndex player, DateTime latestMatchInfo, string? etag, int statusCode)
    {
        if (playersStore.TryGetValue(player, out var info))
        {
            info.LatestCrawl = DateTime.UtcNow;

            // successfule or not modified
            if (statusCode == 200 || statusCode == 304)
            {
                if (info.LatestSuccessfulCrawl > DateTime.MinValue)
                {
                    info.TimesBetweenCrawls.Enqueue(info.LatestCrawl - info.LatestSuccessfulCrawl);
                }
                info.LatestSuccessfulCrawl = info.LatestCrawl;
            }
            else
            {
                if (statusCode == 404 && info.CrawlStatusCodes.LastOrDefault() == 404)
                {
                    RemovePlayer(player);
                }
            }
            if (latestMatchInfo != DateTime.MinValue)
            {
                info.LatestMatchInfo = latestMatchInfo;
            }
            info.CrawlStatusCodes.Enqueue(statusCode);
            info.LatestCrawlStatusCode = statusCode;
            info.Etag = ExtractEtag(etag);
        }
    }

    public async Task<List<PlayerIndex>> GetPlayers()
    {
        if (playersStore.Count == 0)
        {
            var players = await GetPlayersFromArcade();
            // var players = GetPlayersFromCsv();

            foreach (var player in players)
            {
                playersStore.TryAdd(player, new());
            }
        }

        return playersStore
            .OrderBy(o => o.Value.LatestSuccessfulCrawl)
            .Select(s => s.Key)
            .ToList();
    }

    private void RemovePlayer(PlayerIndex player)
    {
        playersStore.TryRemove(player, out var _);
    }

    private async Task<List<PlayerIndex>> GetPlayersFromArcade()
    {
        using var connection = new MySqlConnection(importOptions.Value.ArcadeConnectionString);
        await connection.OpenAsync();
        using var command =
            new MySqlCommand("SELECT ProfileId, RegionId, RealmId FROM ArcadePlayers where ProfileId > 0 and RegionId > 0 and RealmId > 0;"
            , connection);
        using var reader = await command.ExecuteReaderAsync();

        List<PlayerIndex> players = new();
        while (await reader.ReadAsync())
        {
            var toonId = reader.GetInt32(0);
            var regionId = reader.GetInt32(1);
            var realmId = reader.GetInt32(2);
            players.Add(new(toonId, regionId, realmId));
        }
        return players;
    }

    private async Task<List<PlayerIndex>> GetPlayersFromDsstats()
    {
        using var connection = new MySqlConnection(importOptions.Value.DsstatsConnectionString);
        await connection.OpenAsync();
        using var command =
            new MySqlCommand("SELECT ToonId, RegionId, RealmId FROM Players where ToonId > 0 and RegionId > 0 and RealmId > 0;"
            , connection);
        using var reader = await command.ExecuteReaderAsync();

        List<PlayerIndex> players = new();
        while (await reader.ReadAsync())
        {
            var toonId = reader.GetInt32(0);
            var regionId = reader.GetInt32(1);
            var realmId = reader.GetInt32(2);
            players.Add(new(toonId, regionId, realmId));
        }
        return players;
    }

    private List<PlayerIndex> GetPlayersFromCsv()
    {
        // /data/ds/players_test.csv"
        using var reader = new StreamReader("/data/ds/players_test.csv");
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<CsvPlayer>();
        return records.Select(s => new PlayerIndex(s.ToonId, s.RegionId, s.RealmId)).ToList();
    }

    public void LogCrawlStatus(int retryCount)
    {
        lock (lockobject)
        {
            var crawlStatusCounts = from i in playersStore.Values
                                    group i by i.LatestCrawlStatusCode into g
                                    select new
                                    {
                                        CrawlStatusCode = g.Key,
                                        Count = g.Count(),
                                    };

            var lastTimesBetweenCrawls = playersStore.Values
                        .Where(x => x.TimesBetweenCrawls.Count > 0)
                        .Select(s => s.TimesBetweenCrawls.Last())
                        .ToList();


            //var alwaysFailed = playersStore.Values
            //    .Where(x => x.CrawlStatusCodes.Count > 1
            //        && x.CrawlStatusCodes.All(a => a != 200 && a != 304))
            //    .ToList();

            StringBuilder sb = new();
            sb.AppendLine($"retry queue: {retryCount}");
            sb.Append(string.Join(Environment.NewLine, crawlStatusCounts.Select(s => $"StatusCode: {s.CrawlStatusCode} - {s.Count}")));
            if (lastTimesBetweenCrawls.Count > 0)
            {
                sb.AppendLine();
                var maxTime = lastTimesBetweenCrawls.Max();
                var minTime = lastTimesBetweenCrawls.Min();
                var avgTime = TimeSpan.FromTicks((long)lastTimesBetweenCrawls.Average(a => a.Ticks));
                sb.AppendLine($"Times between crawls:");
                sb.AppendLine($"\tMax: {maxTime.ToString(@"hh\:mm\:ss")}");
                sb.AppendLine($"\tMin: {minTime.ToString(@"hh\:mm\:ss")}");
                sb.AppendLine($"\tAvg: {avgTime.ToString(@"hh\:mm\:ss")}");
            }
            //if (alwaysFailed.Count > 0)
            //{
            //    sb.AppendLine();
            //    sb.AppendLine("always failed players:");
            //    sb.Append(string.Join(Environment.NewLine, alwaysFailed.Select(s => $"{s.PlayerId}: {string.Join(',', s.CrawlStatusCodes)}")));
            //}
            sb.AppendLine();

            File.AppendAllText(importOptions.Value.LogFile, sb.ToString());
        }
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

internal class PlayerCrawlInfo
{
    public int PlayerId { get; set; }
    public string? Etag { get; set; }
    public DateTime LatestMatchInfo { get; set; }
    public DateTime LatestSuccessfulCrawl { get; set; }
    public DateTime LatestCrawl { get; set; }
    public Queue<int> CrawlStatusCodes { get; set; } = new Queue<int>(10);
    public int LatestCrawlStatusCode { get; set; }
    public Queue<TimeSpan> TimesBetweenCrawls { get; set; } = new Queue<TimeSpan>(10);
}